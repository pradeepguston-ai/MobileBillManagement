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

namespace MobileBill.IntegrationTests.Identity;

public sealed class PasswordResetApiTests
{
    private const string OldPassword = "old-password-1";
    private const string NewPassword = "new-password-2";

    [Fact]
    public async Task New_password_only_works_after_an_administrator_approves_the_request()
    {
        using var fixture = new ResetApiFixture();
        var email = await fixture.RegisterActiveUserAsync();

        var request = await fixture.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);

        // Nothing changes until the administrator decides.
        Assert.Equal(HttpStatusCode.OK, (await Login(fixture, email, OldPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(fixture, email, NewPassword)).StatusCode);

        TestAuth.Authorize(fixture.Client, UserRole.Administrator);
        var pending = await fixture.Client.GetFromJsonAsync<List<ResetItem>>("/api/users/password-resets");
        var item = Assert.Single(pending!);
        Assert.Equal(email, item.Email);
        Assert.Equal("ITEngineer", item.Role);

        var approve = await fixture.Client.PostAsync($"/api/users/password-resets/{item.Id}/approve", null);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await Login(fixture, email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(fixture, email, OldPassword)).StatusCode);
        Assert.Empty((await fixture.Client.GetFromJsonAsync<List<ResetItem>>("/api/users/password-resets"))!);
    }

    [Fact]
    public async Task Rejected_request_keeps_the_current_password()
    {
        using var fixture = new ResetApiFixture();
        var email = await fixture.RegisterActiveUserAsync();
        await fixture.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email, newPassword = NewPassword });

        TestAuth.Authorize(fixture.Client, UserRole.Administrator);
        var item = Assert.Single((await fixture.Client.GetFromJsonAsync<List<ResetItem>>("/api/users/password-resets"))!);
        var reject = await fixture.Client.PostAsync($"/api/users/password-resets/{item.Id}/reject", null);

        Assert.Equal(HttpStatusCode.NoContent, reject.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(fixture, email, OldPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(fixture, email, NewPassword)).StatusCode);
        var again = await fixture.Client.PostAsync($"/api/users/password-resets/{item.Id}/approve", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_newer_request_replaces_the_earlier_pending_one()
    {
        using var fixture = new ResetApiFixture();
        var email = await fixture.RegisterActiveUserAsync();
        await fixture.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email, newPassword = "first-attempt-1" });
        await fixture.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email, newPassword = NewPassword });

        TestAuth.Authorize(fixture.Client, UserRole.Administrator);
        var item = Assert.Single((await fixture.Client.GetFromJsonAsync<List<ResetItem>>("/api/users/password-resets"))!);
        await fixture.Client.PostAsync($"/api/users/password-resets/{item.Id}/approve", null);

        Assert.Equal(HttpStatusCode.OK, (await Login(fixture, email, NewPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(fixture, email, "first-attempt-1")).StatusCode);
    }

    [Fact]
    public async Task Unknown_and_inactive_accounts_get_the_same_response_but_create_no_request()
    {
        using var fixture = new ResetApiFixture();
        var pendingEmail = await fixture.RegisterPendingUserAsync();

        var unknown = await fixture.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@example.com", newPassword = NewPassword });
        var pending = await fixture.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email = pendingEmail, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, pending.StatusCode);
        TestAuth.Authorize(fixture.Client, UserRole.Administrator);
        Assert.Empty((await fixture.Client.GetFromJsonAsync<List<ResetItem>>("/api/users/password-resets"))!);
    }

    [Fact]
    public async Task Short_new_password_is_rejected()
    {
        using var fixture = new ResetApiFixture();
        var email = await fixture.RegisterActiveUserAsync();

        var response = await fixture.Client.PostAsJsonAsync("/api/auth/forgot-password", new { email, newPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.ITEngineer)]
    [InlineData(UserRole.HeadOfIt)]
    [InlineData(UserRole.GroupHrManager)]
    [InlineData(UserRole.Cfo)]
    public async Task Only_administrators_can_review_reset_requests(UserRole role)
    {
        using var fixture = new ResetApiFixture();
        TestAuth.Authorize(fixture.Client, role);

        var list = await fixture.Client.GetAsync("/api/users/password-resets");
        var approve = await fixture.Client.PostAsync($"/api/users/password-resets/{Guid.NewGuid()}/approve", null);

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, approve.StatusCode);
    }

    private static async Task<HttpResponseMessage> Login(ResetApiFixture fixture, string email, string password)
    {
        using var anonymous = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { email, password }) };
        anonymous.Headers.Authorization = null;
        return await fixture.Client.SendAsync(anonymous);
    }

    private sealed record ResetItem(Guid Id, Guid UserId, string Email, string DisplayName, string Role, DateTimeOffset RequestedAtUtc);

    private sealed class ResetApiFixture : IDisposable
    {
        private readonly WebApplicationFactory<Program> factory;
        public HttpClient Client { get; }

        public ResetApiFixture()
        {
            var databaseName = $"reset-{Guid.NewGuid():N}";
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MobileBillDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MobileBillDbContext>>();
                services.RemoveAll<MobileBillDbContext>();
                services.AddDbContext<MobileBillDbContext>(options => options.UseInMemoryDatabase(databaseName));
            }));
            Client = factory.CreateClient();
        }

        public async Task<string> RegisterPendingUserAsync()
        {
            var email = $"user-{Guid.NewGuid():N}@example.com";
            var response = await Client.PostAsJsonAsync("/api/auth/register", new { email, password = OldPassword, displayName = "Reset User", requestedRole = "ITEngineer" });
            response.EnsureSuccessStatusCode();
            return email;
        }

        public async Task<string> RegisterActiveUserAsync()
        {
            var email = await RegisterPendingUserAsync();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var user = await db.Users.SingleAsync(x => x.Email == email);
            user.Status = UserAccountStatus.Active;
            await db.SaveChangesAsync();
            return email;
        }

        public void Dispose() { Client.Dispose(); factory.Dispose(); }
    }
}
