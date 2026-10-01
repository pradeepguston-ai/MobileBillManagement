using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.IntegrationTests.Identity;

public sealed class AuthApiTests
{
    [Fact]
    public async Task Registered_account_is_pending_and_cannot_log_in_until_activated()
    {
        using var fixture = new AuthApiFixture();
        var email = $"engineer-{Guid.NewGuid():N}@example.com";

        var registerResponse = await fixture.Client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "correct-horse-battery", displayName = "Engineer One", requestedRole = "ITEngineer" });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginBeforeActivation = await fixture.Client.PostAsJsonAsync("/api/auth/login", new { email, password = "correct-horse-battery" });
        Assert.Equal(HttpStatusCode.Forbidden, loginBeforeActivation.StatusCode);

        await fixture.ActivateAsync(email);
        var loginAfterActivation = await fixture.Client.PostAsJsonAsync("/api/auth/login", new { email, password = "correct-horse-battery" });
        Assert.Equal(HttpStatusCode.OK, loginAfterActivation.StatusCode);
        var result = await loginAfterActivation.Content.ReadFromJsonAsync<LoginResult>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal("ITEngineer", result.User.Role);
        Assert.Equal("Active", result.User.Status);
    }

    [Fact]
    public async Task Duplicate_email_registration_is_rejected()
    {
        using var fixture = new AuthApiFixture();
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await fixture.Client.PostAsJsonAsync("/api/auth/register", new { email, password = "correct-horse-battery", displayName = "First", requestedRole = "ITEngineer" });

        var second = await fixture.Client.PostAsJsonAsync("/api/auth/register", new { email, password = "another-password", displayName = "Second", requestedRole = "HeadOfIt" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Administrator_role_cannot_be_self_registered()
    {
        using var fixture = new AuthApiFixture();

        var response = await fixture.Client.PostAsJsonAsync("/api/auth/register",
            new { email = $"admin-{Guid.NewGuid():N}@example.com", password = "correct-horse-battery", displayName = "Wannabe Admin", requestedRole = "Administrator" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_rejected()
    {
        using var fixture = new AuthApiFixture();
        var email = $"wrongpw-{Guid.NewGuid():N}@example.com";
        await fixture.Client.PostAsJsonAsync("/api/auth/register", new { email, password = "correct-horse-battery", displayName = "Someone", requestedRole = "ITEngineer" });
        await fixture.ActivateAsync(email);

        var response = await fixture.Client.PostAsJsonAsync("/api/auth/login", new { email, password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record LoginResult(string Token, LoginUser User);
    private sealed record LoginUser(string Email, string DisplayName, string Role, string Status);

    private sealed class AuthApiFixture : IDisposable
    {
        private readonly WebApplicationFactory<Program> factory;
        public HttpClient Client { get; }

        public AuthApiFixture()
        {
            var databaseName = $"auth-{Guid.NewGuid():N}";
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MobileBillDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MobileBillDbContext>>();
                services.RemoveAll<MobileBillDbContext>();
                services.AddDbContext<MobileBillDbContext>(options => options.UseInMemoryDatabase(databaseName));
            }));
            Client = factory.CreateClient();
        }

        public async Task ActivateAsync(string email)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var user = await db.Users.SingleAsync(x => x.Email == email.ToLowerInvariant());
            user.Status = UserAccountStatus.Active;
            await db.SaveChangesAsync();
        }

        public void Dispose() { Client.Dispose(); factory.Dispose(); }
    }
}
