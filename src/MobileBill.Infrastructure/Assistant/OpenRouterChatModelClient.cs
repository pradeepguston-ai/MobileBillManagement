using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MobileBill.Application.Assistant;

namespace MobileBill.Infrastructure.Assistant;

// Settings come from configuration ("OpenRouter" section). The API key belongs in user secrets, the git-ignored
// appsettings.Development.json or the OpenRouter__ApiKey environment variable, never in a committed file.
public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/";
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "nvidia/nemotron-3-super-120b-a12b:free";
    // Used by OpenRouter when the main model is busy, rate limited or unavailable. Empty for none.
    public string? FallbackModel { get; set; } = "google/gemma-4-31b-it:free";
    public int MaxTokens { get; set; } = 1500;
    public int TimeoutSeconds { get; set; } = 90;
}

// OpenRouter's OpenAI-compatible chat completions endpoint, with tool calling.
public sealed class OpenRouterChatModelClient(HttpClient http, IOptions<OpenRouterOptions> options, ILogger<OpenRouterChatModelClient> logger) : IChatModelClient
{
    private readonly OpenRouterOptions settings = options.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.ApiKey);

    public async Task<ChatModelResult> CompleteAsync(IReadOnlyList<ChatModelMessage> messages, IReadOnlyList<ChatToolDefinition> tools, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new AssistantUnavailableException("The assistant is not set up yet. Ask an administrator to add the OpenRouter API key.");

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.BaseUrl), "chat/completions"))
        {
            Content = JsonContent.Create(BuildBody(messages, tools)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Headers.Add("X-Title", "Mobile Bill Management");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "OpenRouter could not be reached.");
            throw new AssistantUnavailableException("The assistant could not reach the AI service. Try again shortly.");
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode) throw Failure(response.StatusCode);
            JsonDocument document;
            try { document = JsonDocument.Parse(text); }
            catch (JsonException) { throw Failure(HttpStatusCode.BadGateway); }
            using var _ = document;
            // OpenRouter can report a provider error inside a 200 response.
            if (document.RootElement.TryGetProperty("error", out var error)) throw Failure(StatusOf(error));
            return Parse(document.RootElement);
        }
    }

    private Dictionary<string, object?> BuildBody(IReadOnlyList<ChatModelMessage> messages, IReadOnlyList<ChatToolDefinition> tools)
    {
        var body = new Dictionary<string, object?>
        {
            ["messages"] = messages.Select(ToJson).ToList(),
            ["temperature"] = 0.2,
            ["max_tokens"] = settings.MaxTokens,
        };
        // With a fallback, OpenRouter tries the models in order.
        if (string.IsNullOrWhiteSpace(settings.FallbackModel)) body["model"] = settings.Model;
        else body["models"] = new[] { settings.Model, settings.FallbackModel };
        if (tools.Count > 0)
            body["tools"] = tools.Select(tool => new { type = "function", function = new { name = tool.Name, description = tool.Description, parameters = tool.Parameters } }).ToList();
        return body;
    }

    private static Dictionary<string, object?> ToJson(ChatModelMessage message)
    {
        var json = new Dictionary<string, object?> { ["role"] = message.Role, ["content"] = message.Content ?? string.Empty };
        if (message.ToolCalls is { Count: > 0 } calls)
            json["tool_calls"] = calls.Select(call => new { id = call.Id, type = "function", function = new { name = call.Name, arguments = call.ArgumentsJson } }).ToList();
        if (message.ToolCallId is not null) json["tool_call_id"] = message.ToolCallId;
        return json;
    }

    private static ChatModelResult Parse(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            throw new AssistantUnavailableException("The AI service returned an empty answer. Try again.");
        var message = choices[0].GetProperty("message");
        var content = message.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.String ? contentElement.GetString() : null;
        var calls = new List<ChatToolCall>();
        if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in toolCalls.EnumerateArray())
            {
                var function = call.GetProperty("function");
                var arguments = function.TryGetProperty("arguments", out var argumentsElement)
                    ? argumentsElement.ValueKind == JsonValueKind.String ? argumentsElement.GetString() : argumentsElement.GetRawText()
                    : null;
                calls.Add(new ChatToolCall(
                    call.TryGetProperty("id", out var id) ? id.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N"),
                    function.GetProperty("name").GetString() ?? string.Empty,
                    string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments));
            }
        }
        return new ChatModelResult(content, calls);
    }

    private static HttpStatusCode StatusOf(JsonElement error) =>
        error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number ? (HttpStatusCode)code.GetInt32() : HttpStatusCode.BadGateway;

    private AssistantUnavailableException Failure(HttpStatusCode status)
    {
        // The response body can echo the request, so only the status is logged.
        logger.LogWarning("OpenRouter returned {Status}.", (int)status);
        return new AssistantUnavailableException(status switch
        {
            HttpStatusCode.TooManyRequests => "The free AI model is busy or today's free limit has been used. Try again in a minute.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "The assistant's OpenRouter API key was not accepted. Ask an administrator to check it.",
            HttpStatusCode.PaymentRequired => "The OpenRouter account has run out of credit for this model.",
            _ => "The AI service could not answer just now. Try again shortly.",
        });
    }
}
