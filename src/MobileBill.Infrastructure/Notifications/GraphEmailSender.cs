using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MobileBill.Application.Notifications;

namespace MobileBill.Infrastructure.Notifications;

// Sends mail through Microsoft Graph as the sender mailbox, signed in with the OAuth 2.0 client credentials flow.
// The access token is cached until shortly before it expires. The secret and the token are never logged.
public sealed class GraphEmailSender(HttpClient http, IMemoryCache cache, IOptions<EmailOptions> options, ILogger<GraphEmailSender> logger) : IEmailSender
{
    private const string TokenCacheKey = "graph-mail-token";
    private const string Scope = "https://graph.microsoft.com/.default";
    // Base64 adds a third, so 2.9 MB of files keeps the whole request under Graph's 4 MB limit.
    public const long MaxAttachmentBytes = 2_900_000;
    private readonly EmailOptions settings = options.Value;

    public bool IsConfigured => settings.IsConfigured;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new EmailDeliveryException("Email is not set up.", isTransient: false);
        var attachments = message.Attachments ?? [];
        // With Mail.Send alone, attachments must travel inside the one sendMail request, which Graph caps at 4 MB.
        var attachmentBytes = attachments.Sum(attachment => (long)attachment.Content.Length);
        if (attachmentBytes > MaxAttachmentBytes)
            throw new EmailDeliveryException($"The attachments are {attachmentBytes / 1024d / 1024d:0.0} MB; an email can carry at most {MaxAttachmentBytes / 1024d / 1024d:0.0} MB.", isTransient: false);
        var token = await TokenAsync(cancellationToken);
        var body = new Dictionary<string, object>
        {
            ["message"] = new Dictionary<string, object>
            {
                ["subject"] = message.Subject,
                ["body"] = new { contentType = "HTML", content = message.HtmlBody },
                ["toRecipients"] = message.To.Select(address => new { emailAddress = new { address } }),
                ["attachments"] = attachments.Select(attachment => new Dictionary<string, object>
                {
                    ["@odata.type"] = "#microsoft.graph.fileAttachment",
                    ["name"] = attachment.FileName,
                    ["contentType"] = attachment.ContentType,
                    ["contentBytes"] = Convert.ToBase64String(attachment.Content),
                }),
            },
            ["saveToSentItems"] = true,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.GraphBaseUrl), $"users/{Uri.EscapeDataString(settings.SenderMailbox)}/sendMail")) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await SendRequestAsync(request, "Microsoft Graph", cancellationToken);
        if (response.IsSuccessStatusCode) return;
        // A token revoked or expired early: fetch a new one on the next attempt.
        if (response.StatusCode == HttpStatusCode.Unauthorized) cache.Remove(TokenCacheKey);
        throw await FailureAsync(response, "Microsoft Graph refused the email", transient: response.StatusCode == HttpStatusCode.Unauthorized || IsTransient(response.StatusCode), cancellationToken);
    }

    private async Task<string> TokenAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(TokenCacheKey, out string? cached) && cached is not null) return cached;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.LoginBaseUrl), $"{Uri.EscapeDataString(settings.TenantId!)}/oauth2/v2.0/token"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = settings.ClientId!,
                ["client_secret"] = settings.ClientSecret!,
                ["scope"] = Scope,
                ["grant_type"] = "client_credentials",
            }),
        };
        using var response = await SendRequestAsync(request, "Microsoft sign-in", cancellationToken);
        // A wrong tenant, client ID or secret will not come right by trying again.
        if (!response.IsSuccessStatusCode) throw await FailureAsync(response, "Microsoft sign-in refused the mail credentials", IsTransient(response.StatusCode), cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var token = document.RootElement.GetProperty("access_token").GetString() ?? throw new EmailDeliveryException("Microsoft sign-in returned no access token.", isTransient: true);
        var lifetime = document.RootElement.TryGetProperty("expires_in", out var expiresIn) && expiresIn.TryGetInt32(out var seconds) ? seconds : 3600;
        cache.Set(TokenCacheKey, token, TimeSpan.FromSeconds(Math.Max(60, lifetime - 300)));
        return token;
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpRequestMessage request, string service, CancellationToken cancellationToken)
    {
        try { return await http.SendAsync(request, cancellationToken); }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new EmailDeliveryException($"{service} could not be reached.", isTransient: true, inner: exception);
        }
    }

    private async Task<EmailDeliveryException> FailureAsync(HttpResponseMessage response, string what, bool transient, CancellationToken cancellationToken)
    {
        var code = await ErrorCodeAsync(response, cancellationToken);
        logger.LogWarning("{What}: HTTP {Status} {Code}.", what, (int)response.StatusCode, code);
        var retryAfter = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
        return new EmailDeliveryException($"{what} (HTTP {(int)response.StatusCode} {code}).", transient, retryAfter);
    }

    // Graph answers { "error": { "code": ... } }; the sign-in service answers { "error": "invalid_client", ... }.
    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!document.RootElement.TryGetProperty("error", out var error)) return string.Empty;
            return error.ValueKind == JsonValueKind.Object && error.TryGetProperty("code", out var code) ? code.GetString() ?? string.Empty : error.ValueKind == JsonValueKind.String ? error.GetString() ?? string.Empty : string.Empty;
        }
        catch (JsonException) { return string.Empty; }
    }

    private static bool IsTransient(HttpStatusCode status) => status is HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout || (int)status >= 500;
}
