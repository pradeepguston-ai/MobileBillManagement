using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.IntegrationTests.Identity;

public sealed class ActivityLogApiTests : IDisposable
{
    private readonly WebApplicationFactory<Program> factory;

    public ActivityLogApiTests()
    {
        var databaseName = $"activity-{Guid.NewGuid():N}";
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ActivityLog:Enabled"] = "true" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MobileBillDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MobileBillDbContext>>();
                services.RemoveAll<MobileBillDbContext>();
                services.AddDbContext<MobileBillDbContext>(options => options.UseInMemoryDatabase(databaseName));
            });
        });
    }

    [Fact]
    public async Task Successful_changes_are_recorded_with_who_made_them_and_every_user_can_read_them()
    {
        using var editor = factory.CreateClient();
        TestAuth.Authorize(editor, UserRole.ITEngineer, "Pradeep Dissanayaka");

        var created = await editor.PostAsJsonAsync("/api/factories", new { code = "CAPL", name = "Concord Apparel", isActive = true });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var factoryId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        // A refused change and a page view are not recorded.
        Assert.False((await editor.PostAsJsonAsync("/api/factories", new { code = "", name = "", isActive = true })).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync("/api/factories")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await editor.PostAsync($"/api/factories/{factoryId}/deactivate", null)).StatusCode);

        using var viewer = factory.CreateClient();
        TestAuth.Authorize(viewer, UserRole.Viewer, "Common Account");
        var log = await viewer.GetFromJsonAsync<JsonElement>("/api/activity-log");

        var items = log.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["Deactivated factory Concord Apparel (CAPL)", "Created factory Concord Apparel (CAPL)"], items.Select(item => item.GetProperty("description").GetString()));
        Assert.All(items, item => Assert.Equal(("Pradeep Dissanayaka", "ITEngineer", "Masters"), (item.GetProperty("userName").GetString(), item.GetProperty("role").GetString(), item.GetProperty("area").GetString())));
        // Reading the log is not recorded either.
        Assert.Equal(2, (await viewer.GetFromJsonAsync<JsonElement>("/api/activity-log")).GetProperty("totalCount").GetInt32());

        var filters = await viewer.GetFromJsonAsync<JsonElement>("/api/activity-log/filters");
        Assert.Equal("Pradeep Dissanayaka", filters.GetProperty("users")[0].GetProperty("userName").GetString());
        Assert.Equal("Masters", filters.GetProperty("areas")[0].GetString());
    }

    [Fact]
    public async Task The_log_needs_a_signed_in_user()
    {
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/activity-log")).StatusCode);
    }

    public void Dispose() => factory.Dispose();
}
