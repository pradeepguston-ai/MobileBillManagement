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
    public async Task Administrator_creates_an_active_account_that_can_sign_in()
    {
        using var fixture = new UsersApiFixture();
        TestAuth.Authorize(fixture.Client, UserRole.Administrator);

        var create = await fixture.Client.PostAsJsonAsync("/api/users", new { email = "  New.User@Concord.lk ", displayName = " New User ", role = "Administrator", password = "Str0ngPass!" });

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<ManagedUserItem>();
        Assert.Equal(("new.user@concord.lk", "New User", "Administrator", "Active"), (created!.Email, created.DisplayName, created.Role, created.Status));

        fixture.Client.DefaultRequestHeaders.Authorization = null;
        var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new { email = "new.user@concord.lk", password = "Str0ngPass!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task A_created_account_must_replace_its_one_time_password_before_using_the_system()
    {
        using var fixture = new UsersApiFixture();
        TestAuth.Authorize(fixture.Client, UserRole.Administrator);
        var created = await (await fixture.Client.PostAsJsonAsync("/api/users", new { email = "otp@concord.lk", displayName = "Otp User", role = "Viewer", password = "OneTime#123" })).Content.ReadFromJsonAsync<ManagedUserItem>();
        Assert.True(created!.MustChangePassword);

        fixture.Client.DefaultRequestHeaders.Authorization = null;
        var login = await (await fixture.Client.PostAsJsonAsync("/api/auth/login", new { email = "otp@concord.lk", password = "OneTime#123" })).Content.ReadFromJsonAsync<AuthItem>();
        Assert.True(login!.User.MustChangePassword);
        fixture.Client.DefaultRequestHeaders.Authorization = new("Bearer", login.Token);

        // Only reading their own account and changing the password are allowed with the one-time password's token.
        var blocked = await fixture.Client.GetAsync("/api/permissions/mine");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Contains("Password change required", await blocked.Content.ReadAsStringAsync());
        Assert.True((await fixture.Client.GetFromJsonAsync<UserItem>("/api/auth/me"))!.MustChangePassword);

        var wrong = await fixture.Client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "Wrong#123", newPassword = "MyOwn#Pass1" });
        var same = await fixture.Client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "OneTime#123", newPassword = "OneTime#123" });
        var tooShort = await fixture.Client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "OneTime#123", newPassword = "short" });
        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (wrong.StatusCode, same.StatusCode, tooShort.StatusCode));
        Assert.Contains("one-time password is incorrect", await wrong.Content.ReadAsStringAsync());

        var changed = await fixture.Client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "OneTime#123", newPassword = "MyOwn#Pass1" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var fresh = await changed.Content.ReadFromJsonAsync<AuthItem>();
        Assert.False(fresh!.User.MustChangePassword);
        fixture.Client.DefaultRequestHeaders.Authorization = new("Bearer", fresh.Token);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/permissions/mine")).StatusCode);

        fixture.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/auth/login", new { email = "otp@concord.lk", password = "OneTime#123" })).StatusCode);
        var again = await (await fixture.Client.PostAsJsonAsync("/api/auth/login", new { email = "otp@concord.lk", password = "MyOwn#Pass1" })).Content.ReadFromJsonAsync<AuthItem>();
        Assert.False(again!.User.MustChangePassword);
        TestAuth.Authorize(fixture.Client, UserRole.Administrator);
        Assert.False((await fixture.Client.GetFromJsonAsync<List<ManagedUserItem>>("/api/users"))!.Single(x => x.Email == "otp@concord.lk").MustChangePassword);
    }

    [Fact]
    public async Task An_approved_password_reset_replaces_a_one_time_password()
    {
        using var fixture = new UsersApiFixture();
        TestAuth.Authorize(fixture.Client, UserRole.Administrator);
        await fixture.Client.PostAsJsonAsync("/api/users", new { email = "forgot@concord.lk", displayName = "Forgot User", role = "Viewer", password = "OneTime#123" });
        fixture.Client.DefaultRequestHeaders.Authorization = null;
        await fixture.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "forgot@concord.lk", newPassword = "Chosen#Pass1" });

        TestAuth.Authorize(fixture.Client, UserRole.Administrator);
        var reset = (await fixture.Client.GetFromJsonAsync<List<ResetItem>>("/api/users/password-resets"))!.Single();
        Assert.Equal(HttpStatusCode.NoContent, (await fixture.Client.PostAsync($"/api/users/password-resets/{reset.Id}/approve", null)).StatusCode);

        fixture.Client.DefaultRequestHeaders.Authorization = null;
        var login = await (await fixture.Client.PostAsJsonAsync("/api/auth/login", new { email = "forgot@concord.lk", password = "Chosen#Pass1" })).Content.ReadFromJsonAsync<AuthItem>();
        Assert.False(login!.User.MustChangePassword);
    }

    [Fact]
    public async Task Creating_an_account_rejects_a_taken_email_and_a_short_password()
    {
        using var fixture = new UsersApiFixture();
        await fixture.SeedPendingUserAsync("taken@concord.lk", UserRole.Viewer, UserAccountStatus.Active);
        TestAuth.Authorize(fixture.Client, UserRole.Administrator);

        var taken = await fixture.Client.PostAsJsonAsync("/api/users", new { email = "TAKEN@concord.lk", displayName = "Someone", role = "Viewer", password = "Str0ngPass!" });
        var shortPassword = await fixture.Client.PostAsJsonAsync("/api/users", new { email = "other@concord.lk", displayName = "Someone", role = "Viewer", password = "short" });

        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, shortPassword.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.ITEngineer)]
    [InlineData(UserRole.HeadOfIt)]
    public async Task Only_an_administrator_can_create_accounts(UserRole role)
    {
        using var fixture = new UsersApiFixture();
        TestAuth.Authorize(fixture.Client, role);

        var response = await fixture.Client.PostAsJsonAsync("/api/users", new { email = "x@concord.lk", displayName = "X", role = "Viewer", password = "Str0ngPass!" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

    private sealed record ManagedUserItem(Guid Id, string Email, string DisplayName, string Role, string Status, DateTimeOffset RegisteredAtUtc, DateTimeOffset? UpdatedAtUtc, bool MustChangePassword);
    private sealed record PendingUserItem(Guid Id, string Email, string DisplayName, string RequestedRole, DateTimeOffset RegisteredAtUtc);
    private sealed record UserItem(Guid Id, string Email, string DisplayName, string Role, string Status, bool MustChangePassword);
    private sealed record AuthItem(string Token, UserItem User);
    private sealed record ResetItem(Guid Id, Guid UserId, string Email);

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
