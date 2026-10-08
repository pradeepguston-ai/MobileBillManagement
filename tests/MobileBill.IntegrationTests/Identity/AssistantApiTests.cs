using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Application.Assistant;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.IntegrationTests.Identity;

public sealed class AssistantApiTests
{
    [Fact]
    public async Task Without_an_api_key_the_assistant_reports_itself_off_and_refuses_questions()
    {
        using var factory = Factory(model: null);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/assistant/status")).StatusCode);
        TestAuth.Authorize(client, UserRole.HrUser);
        Assert.False((await client.GetFromJsonAsync<AssistantStatusDto>("/api/assistant/status"))!.IsEnabled);

        var response = await client.PostAsJsonAsync("/api/assistant/ask", new AssistantAskRequest("Hello"));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("not set up yet", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Every_role_can_ask_and_each_user_is_limited_to_ten_questions_a_minute()
    {
        using var factory = Factory(new EchoModel());
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.FinanceUser, userId: Guid.NewGuid());

        Assert.True((await client.GetFromJsonAsync<AssistantStatusDto>("/api/assistant/status"))!.IsEnabled);
        var reply = await (await client.PostAsJsonAsync("/api/assistant/ask", new AssistantAskRequest("Hi"))).Content.ReadFromJsonAsync<AssistantReplyDto>();
        Assert.Equal("Hello from the model", reply!.Reply);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/assistant/ask", new AssistantAskRequest(" "))).StatusCode);

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 9; i++) statuses.Add((await client.PostAsJsonAsync("/api/assistant/ask", new AssistantAskRequest("Again"))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);   // the 11th request in the minute

        TestAuth.Authorize(client, UserRole.FinanceUser, userId: Guid.NewGuid());   // someone else still can
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/assistant/ask", new AssistantAskRequest("Hi"))).StatusCode);
    }

    private static WebApplicationFactory<Program> Factory(IChatModelClient? model)
    {
        var databaseName = $"assistant-{Guid.NewGuid():N}";
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Never use a real OpenRouter key from local settings in tests.
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["OpenRouter:ApiKey"] = "" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MobileBillDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MobileBillDbContext>>();
                services.RemoveAll<MobileBillDbContext>();
                services.AddDbContext<MobileBillDbContext>(options => options.UseInMemoryDatabase(databaseName));
                if (model is not null)
                {
                    services.RemoveAll<IChatModelClient>();
                    services.AddSingleton(model);
                }
            });
        });
    }

    private sealed class EchoModel : IChatModelClient
    {
        public bool IsConfigured => true;
        public Task<ChatModelResult> CompleteAsync(IReadOnlyList<ChatModelMessage> messages, IReadOnlyList<ChatToolDefinition> tools, CancellationToken cancellationToken) =>
            Task.FromResult(new ChatModelResult("Hello from the model", []));
    }
}
