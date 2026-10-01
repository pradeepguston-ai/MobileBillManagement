using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;
using MobileBill.IntegrationTests;

namespace MobileBill.IntegrationTests.Identity;

public sealed class UserManagementApiTests
{
    [Fact]
    public async Task Administrator_can_list_and_activate_a_pending_user()
    {
        using var fixture = new UsersApiFixture();
        var pendingId = await fixture.SeedPendingUserAsync("pending@example.com", UserRole.ITEngineer);
        TestAuth.Authorize(fixture.Client, UserRole.Administrator);

        var pendingResponse = await fixture.Client.GetFromJsonAsync<List<PendingUserItem>>("/api/users/pending");
        Assert.NotNull(pendingResponse);
        Assert.Contains(pendingResponse, item => item.Id == pendingId);

        var activateResponse = await fixture.Client.PostAsJsonAsync($"/api/users/{pendingId}/activate", new { roleOverride = (string?)null });
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        var activated = await activateResponse.Content.ReadFromJsonAsync<UserItem>();
        Assert.NotNull(activated);
        Assert.Equal("Active", activated.Status);
    }

    [Fact]
    public async Task Non_administrator_cannot_manage_users()
    {
        using var fixture = new UsersApiFixture();
        var pendingId = await fixture.SeedPendingUserAsync("blocked@example.com", UserRole.ITEngineer);
        TestAuth.Authorize(fixture.Client, UserRole.ITEngineer);

        var response = await fixture.Client.PostAsJsonAsync($"/api/users/{pendingId}/activate", new { roleOverride = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_can_list_all_users_and_change_a_role()
    {
        using var fixture = new UsersApiFixture();
        var pendingId = await fixture.SeedPendingUserAsync("pending@example.com", UserRole.ITEngineer);
        var activeId = await fixture.SeedPendingUserAsync("active@example.com", UserRole.Cfo, UserAccountStatus.Active);
        TestAuth.Authorize(fixture.Client, UserRole.Administrator);

        var all = await fixture.Client.GetFromJsonAsync<List<ManagedUserItem>>("/api/users");
        Assert.Equal(2, all!.Count);
        Assert.Contains(all, user => user.Id == pendingId && user.Status == "PendingActivation");
        var filtered = await fixture.Client.GetFromJsonAsync<List<ManagedUserItem>>("/api/users?search=ACTIVE@");
        Assert.Equal(activeId, Assert.Single(filtered!).Id);

        var change = await fixture.Client.PutAsJsonAsync($"/api/users/{activeId}/role", new { role = "HeadOfIt" });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        var changed = await change.Content.ReadFromJsonAsync<UserItem>();
        Assert.Equal("HeadOfIt", changed!.Role);
        Assert.Equal("Active", changed.Status);
    }

    [Fact]
    public async Task Administrator_cannot_deactivate_or_demote_their_own_account()
    {
        using var fixture = new UsersApiFixture();
        var adminId = await fixture.SeedPendingUserAsync("admin@example.com", UserRole.Administrator, UserAccountStatus.Active);
        TestAuth.Authorize(fixture.Client, UserRole.Administrator, userId: adminId);

        var demote = await fixture.Client.PutAsJsonAsync($"/api/users/{adminId}/role", new { role = "ITEngineer" });
        var deactivate = await fixture.Client.PostAsync($"/api/users/{adminId}/deactivate", null);

        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, deactivate.StatusCode);
    }

    [Fact]
    public async Task Non_administrator_cannot_list_users_or_change_roles()
    {
        using var fixture = new UsersApiFixture();
        var userId = await fixture.SeedPendingUserAsync("someone@example.com", UserRole.ITEngineer, UserAccountStatus.Active);
        TestAuth.Authorize(fixture.Client, UserRole.HeadOfIt);

        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.PutAsJsonAsync($"/api/users/{userId}/role", new { role = "Cfo" })).StatusCode);
    }

    private sealed record ManagedUserItem(Guid Id, string Email, string DisplayName, string Role, string Status, DateTimeOffset RegisteredAtUtc, DateTimeOffset? UpdatedAtUtc);
    private sealed record PendingUserItem(Guid Id, string Email, string DisplayName, string RequestedRole, DateTimeOffset RegisteredAtUtc);
    private sealed record UserItem(Guid Id, string Email, string DisplayName, string Role, string Status);

    private sealed class UsersApiFixture : IDisposable
    {
        private readonly WebApplicationFactory<Program> factory;
        public HttpClient Client { get; }

        public UsersApiFixture()
        {
            var databaseName = $"users-{Guid.NewGuid():N}";
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MobileBillDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MobileBillDbContext>>();
                services.RemoveAll<MobileBillDbContext>();
                services.AddDbContext<MobileBillDbContext>(options => options.UseInMemoryDatabase(databaseName));
            }));
            Client = factory.CreateClient();
        }

        public async Task<Guid> SeedPendingUserAsync(string email, UserRole role, UserAccountStatus status = UserAccountStatus.PendingActivation)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var user = new User { Email = email, DisplayName = "Pending User", PasswordHash = "hash", Role = role, Status = status };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        }

        public void Dispose() { Client.Dispose(); factory.Dispose(); }
    }
}
