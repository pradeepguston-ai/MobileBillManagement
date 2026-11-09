using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MobileBill.Application.Notifications;
using MobileBill.Infrastructure.Notifications;

namespace MobileBill.UnitTests.Notifications;

public sealed class GraphEmailSenderTests
{
    private static readonly EmailMessage Message = new(["hod.it@concord.lk", "cfo@concord.lk"], "[Mobile Billing System] Test", "<p>Hello</p>");

    [Fact]
    public async Task Signs_in_with_client_credentials_and_sends_from_the_sender_mailbox()
    {
        var handler = new FakeHandler(Token(), Accepted());

        await Sender(handler).SendAsync(Message, default);

        var (tokenRequest, tokenBody) = handler.Requests[0];
        Assert.Equal("https://login.microsoftonline.com/tenant-id/oauth2/v2.0/token", tokenRequest.RequestUri!.ToString());
        Assert.Contains("client_id=client-id", tokenBody);
        Assert.Contains("client_secret=secret-value", tokenBody);
        Assert.Contains("grant_type=client_credentials", tokenBody);
        Assert.Contains("scope=https%3A%2F%2Fgraph.microsoft.com%2F.default", tokenBody);

        var (sendRequest, sendBody) = handler.Requests[1];
        Assert.Equal("https://graph.microsoft.com/v1.0/users/noreply.mobilebill%40concord.lk/sendMail", sendRequest.RequestUri!.ToString());
        Assert.Equal("Bearer token-1", sendRequest.Headers.Authorization!.ToString());
        using var json = JsonDocument.Parse(sendBody);
        var message = json.RootElement.GetProperty("message");
        Assert.Equal("[Mobile Billing System] Test", message.GetProperty("subject").GetString());
        Assert.Equal("HTML", message.GetProperty("body").GetProperty("contentType").GetString());
        Assert.Equal(["hod.it@concord.lk", "cfo@concord.lk"], message.GetProperty("toRecipients").EnumerateArray().Select(x => x.GetProperty("emailAddress").GetProperty("address").GetString()));
    }

    [Fact]
    public async Task Sends_attachments_as_graph_file_attachments()
    {
        var handler = new FakeHandler(Token(), Accepted());

        await Sender(handler).SendAsync(Message with { Attachments = [new EmailAttachment("Report.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", [1, 2, 3])] }, default);

        using var json = JsonDocument.Parse(handler.Requests[1].Body);
        var attachment = Assert.Single(json.RootElement.GetProperty("message").GetProperty("attachments").EnumerateArray());
        Assert.Equal("#microsoft.graph.fileAttachment", attachment.GetProperty("@odata.type").GetString());
        Assert.Equal("Report.xlsx", attachment.GetProperty("name").GetString());
        Assert.Equal("AQID", attachment.GetProperty("contentBytes").GetString());
    }

    [Fact]
    public async Task Attachments_over_the_graph_request_limit_are_refused_before_sending()
    {
        var handler = new FakeHandler();
        var tooLarge = new EmailAttachment("Report.pdf", "application/pdf", new byte[GraphEmailSender.MaxAttachmentBytes + 1]);

        var refusal = await Assert.ThrowsAsync<EmailDeliveryException>(() => Sender(handler).SendAsync(Message with { Attachments = [tooLarge] }, default));

        Assert.False(refusal.IsTransient);
        Assert.Contains("at most 2.8 MB", refusal.Message);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Reuses_the_access_token_until_it_expires()
    {
        var handler = new FakeHandler(Token(), Accepted(), Accepted());
        var sender = Sender(handler);

        await sender.SendAsync(Message, default);
        await sender.SendAsync(Message, default);

        Assert.Equal(1, handler.Requests.Count(x => x.Request.RequestUri!.Host == "login.microsoftonline.com"));
    }

    [Fact]
    public async Task A_busy_mail_service_is_a_transient_failure_with_its_retry_after()
    {
        var busy = new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("""{ "error": { "code": "ApplicationThrottled" } }""") };
        busy.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(() => Sender(new FakeHandler(Token(), busy)).SendAsync(Message, default));

        Assert.True(failure.IsTransient);
        Assert.Equal(TimeSpan.FromSeconds(30), failure.RetryAfter);
        Assert.Contains("ApplicationThrottled", failure.Message);
    }

    [Fact]
    public async Task Wrong_credentials_are_not_retried_and_the_secret_is_not_in_the_error()
    {
        var refused = new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("""{ "error": "invalid_client" }""") };

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(() => Sender(new FakeHandler(refused)).SendAsync(Message, default));

        Assert.False(failure.IsTransient);
        Assert.Contains("invalid_client", failure.Message);
        Assert.DoesNotContain("secret-value", failure.Message);
    }

    [Fact]
    public async Task A_rejected_token_is_dropped_so_the_next_attempt_signs_in_again()
    {
        var handler = new FakeHandler(Token("token-1"), new HttpResponseMessage(HttpStatusCode.Unauthorized), Token("token-2"), Accepted());
        var sender = Sender(handler);

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(Message, default));
        Assert.True(failure.IsTransient);
        await sender.SendAsync(Message, default);

        Assert.Equal("Bearer token-2", handler.Requests[3].Request.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Refuses_to_send_until_it_is_configured()
    {
        var sender = new GraphEmailSender(new HttpClient(new FakeHandler()), new MemoryCache(new MemoryCacheOptions()), Options.Create(new EmailOptions { Enabled = true }), NullLogger<GraphEmailSender>.Instance);

        Assert.False(sender.IsConfigured);
        Assert.False((await Assert.ThrowsAsync<EmailDeliveryException>(() => sender.SendAsync(Message, default))).IsTransient);
    }

    private static GraphEmailSender Sender(FakeHandler handler) => new(new HttpClient(handler), new MemoryCache(new MemoryCacheOptions()),
        Options.Create(new EmailOptions { Enabled = true, TenantId = "tenant-id", ClientId = "client-id", ClientSecret = "secret-value" }), NullLogger<GraphEmailSender>.Instance);

    private static HttpResponseMessage Token(string token = "token-1") => new(HttpStatusCode.OK) { Content = new StringContent($$"""{ "access_token": "{{token}}", "expires_in": 3599, "token_type": "Bearer" }""", Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Accepted() => new(HttpStatusCode.Accepted);

    private sealed class FakeHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> pending = new(responses);
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request, request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return pending.Dequeue();
        }
    }
}
