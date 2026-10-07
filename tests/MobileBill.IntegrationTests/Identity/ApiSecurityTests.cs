using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.IntegrationTests.Identity;

public sealed class ApiSecurityTests
{
    private static readonly string BatchId = Guid.NewGuid().ToString();

    public static TheoryData<string> ProtectedReadUrls => new()
    {
        "/api/bill-batches",
        $"/api/bill-batches/{BatchId}",
        $"/api/bill-batches/{BatchId}/lines",
        $"/api/bill-batches/{BatchId}/lines/excel",
        $"/api/bill-batches/{BatchId}/review",
        $"/api/bill-batches/{BatchId}/review/rows",
        $"/api/bill-batches/{BatchId}/exceptions",
        $"/api/bill-batches/{BatchId}/exceptions/summary",
        $"/api/reports/billing/{BatchId}/excel",
        $"/api/reports/billing/{BatchId}/pdf",
        $"/api/bill-batches/{BatchId}/review/rows/{BatchId}/trend",
        "/api/employees/resignations?status=Pending",
        "/api/mobile-packages",
        "/api/mobile-devices",
        "/api/mobile-devices/to-collect",
        "/api/mobile-devices/register/excel",
        "/api/reports/vas/batches",
        $"/api/reports/vas/{BatchId}",
        $"/api/reports/vas/{BatchId}/excel",
        "/api/employees",
        "/api/mobile-accounts",
        "/api/factories",
        "/api/insights",
    };

    [Theory]
    [MemberData(nameof(ProtectedReadUrls))]
    public async Task Anonymous_requests_cannot_read_billing_employee_or_report_data(string url)
    {
        using var fixture = new SecurityApiFixture(validateUserOnEachRequest: false);

        var response = await fixture.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_of_an_active_user_with_an_unchanged_role_is_accepted()
    {
        using var fixture = new SecurityApiFixture(validateUserOnEachRequest: true);
        var userId = await fixture.SeedUserAsync(UserRole.ITEngineer, UserAccountStatus.Active);
        TestAuth.Authorize(fixture.Client, UserRole.ITEngineer, userId: userId);

        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Token_stops_working_as_soon_as_the_user_is_deactivated()
    {
        using var fixture = new SecurityApiFixture(validateUserOnEachRequest: true);
        var userId = await fixture.SeedUserAsync(UserRole.ITEngineer, UserAccountStatus.Active);
        TestAuth.Authorize(fixture.Client, UserRole.ITEngineer, userId: userId);
        await fixture.SetStatusAsync(userId, UserAccountStatus.Deactivated);

        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Token_carrying_an_old_role_is_rejected_after_the_role_changes()
    {
        using var fixture = new SecurityApiFixture(validateUserOnEachRequest: true);
        var userId = await fixture.SeedUserAsync(UserRole.ITEngineer, UserAccountStatus.Active);
        TestAuth.Authorize(fixture.Client, UserRole.Administrator, userId: userId);

        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Token_for_a_user_that_does_not_exist_is_rejected()
    {
        using var fixture = new SecurityApiFixture(validateUserOnEachRequest: true);
        TestAuth.Authorize(fixture.Client, UserRole.Administrator);

        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Repeated_sign_in_attempts_are_rate_limited()
    {
        using var fixture = new SecurityApiFixture(validateUserOnEachRequest: false);
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 11; attempt++)
            statuses.Add((await fixture.Client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = "wrong-password" })).StatusCode);

        Assert.All(statuses.Take(10), status => Assert.Equal(HttpStatusCode.Unauthorized, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    private sealed class SecurityApiFixture : IDisposable
    {
        private readonly WebApplicationFactory<Program> factory;
        public HttpClient Client { get; }

        public SecurityApiFixture(bool validateUserOnEachRequest)
        {
            var databaseName = $"security-{Guid.NewGuid():N}";
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:ValidateUserOnEachRequest"] = validateUserOnEachRequest.ToString()
                }));
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<MobileBillDbContext>>();
                    services.RemoveAll<IDbContextOptionsConfiguration<MobileBillDbContext>>();
                    services.RemoveAll<MobileBillDbContext>();
                    services.AddDbContext<MobileBillDbContext>(options => options.UseInMemoryDatabase(databaseName));
                });
            });
            Client = factory.CreateClient();
        }

        public async Task<Guid> SeedUserAsync(UserRole role, UserAccountStatus status)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var user = new User { Email = $"{Guid.NewGuid():N}@example.com", DisplayName = "Security Test", PasswordHash = "hash", Role = role, Status = status };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        }

        public async Task SetStatusAsync(Guid userId, UserAccountStatus status)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var user = await db.Users.SingleAsync(x => x.Id == userId);
            user.Status = status;
            await db.SaveChangesAsync();
        }

        public void Dispose() { Client.Dispose(); factory.Dispose(); }
    }
}
