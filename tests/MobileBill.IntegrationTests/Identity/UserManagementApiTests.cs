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

        public async Task<Guid> SeedPendingUserAsync(string email, UserRole role)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var user = new User { Email = email, DisplayName = "Pending User", PasswordHash = "hash", Role = role, Status = UserAccountStatus.PendingActivation };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        }

        public void Dispose() { Client.Dispose(); factory.Dispose(); }
    }
}
