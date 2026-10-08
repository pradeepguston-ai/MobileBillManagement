using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MobileBill.Application.Assistant;
using MobileBill.Infrastructure.Assistant;

namespace MobileBill.UnitTests.Assistant;

public sealed class OpenRouterChatModelClientTests
{
    private static readonly ChatToolDefinition Tool = AssistantToolbox.Definitions.Single(tool => tool.Name == "find_allocations");

    [Fact]
    public async Task Sends_the_key_models_messages_and_tools_and_reads_tool_calls()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """
            { "choices": [ { "message": { "role": "assistant", "content": null,
              "tool_calls": [ { "id": "call-1", "type": "function", "function": { "name": "find_allocations", "arguments": "{\"search\":\"MOBILE-1\"}" } } ] } } ] }
            """);
        var client = Client(handler);

        var result = await client.CompleteAsync(
            [ChatModelMessage.System("rules"), ChatModelMessage.User("Who holds MOBILE-1?"), ChatModelMessage.Assistant(null, [new ChatToolCall("c0", "get_sim_pool", "{}")]), ChatModelMessage.Tool("c0", "{\"count\":0}")],
            [Tool], default);

        Assert.Equal((null, "call-1", "find_allocations", "{\"search\":\"MOBILE-1\"}"), (result.Content, result.ToolCalls[0].Id, result.ToolCalls[0].Name, result.ToolCalls[0].ArgumentsJson));
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer test-key", handler.Request.Headers.Authorization!.ToString());
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(["main:free", "backup:free"], body.RootElement.GetProperty("models").EnumerateArray().Select(model => model.GetString()));
        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal(["system", "user", "assistant", "tool"], messages.EnumerateArray().Select(message => message.GetProperty("role").GetString()));
        Assert.Equal("get_sim_pool", messages[2].GetProperty("tool_calls")[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("c0", messages[3].GetProperty("tool_call_id").GetString());
        Assert.Equal("find_allocations", body.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal("search", body.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("parameters").GetProperty("required")[0].GetString());
    }

    [Fact]
    public async Task Reads_a_plain_answer_and_sends_no_tools_when_none_are_offered()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{ "choices": [ { "message": { "role": "assistant", "content": "Hello" } } ] }""");

        var result = await Client(handler, fallback: "").CompleteAsync([ChatModelMessage.User("Hi")], [], default);

        Assert.Equal(("Hello", 0), (result.Content, result.ToolCalls.Count));
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("main:free", body.RootElement.GetProperty("model").GetString());
        Assert.False(body.RootElement.TryGetProperty("tools", out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "{}", "free limit")]
    [InlineData(HttpStatusCode.Unauthorized, "{}", "API key was not accepted")]
    [InlineData(HttpStatusCode.OK, """{ "error": { "code": 429, "message": "Rate limit exceeded" } }""", "free limit")]
    [InlineData(HttpStatusCode.OK, "not json", "could not answer")]
    public async Task Problems_become_a_message_that_can_be_shown(HttpStatusCode status, string response, string expected)
    {
        var failure = await Assert.ThrowsAsync<AssistantUnavailableException>(() => Client(new FakeHandler(status, response)).CompleteAsync([ChatModelMessage.User("Hi")], [], default));
        Assert.Contains(expected, failure.Message);
    }

    [Fact]
    public async Task Without_a_key_it_is_not_configured_and_sends_nothing()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var client = Client(handler, key: null);

        Assert.False(client.IsConfigured);
        await Assert.ThrowsAsync<AssistantUnavailableException>(() => client.CompleteAsync([ChatModelMessage.User("Hi")], [], default));
        Assert.Null(handler.Request);
    }

    private static OpenRouterChatModelClient Client(FakeHandler handler, string? key = "test-key", string fallback = "backup:free") =>
        new(new HttpClient(handler), Options.Create(new OpenRouterOptions { ApiKey = key, Model = "main:free", FallbackModel = fallback }), NullLogger<OpenRouterChatModelClient>.Instance);

    private sealed class FakeHandler(HttpStatusCode status, string response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }
}
