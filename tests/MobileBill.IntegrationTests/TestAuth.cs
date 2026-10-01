using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Identity;

namespace MobileBill.IntegrationTests;

// Matches the Jwt:* values in src/MobileBill.Api/appsettings.Development.json, which is what
// WebApplicationFactory<Program> loads (the test host runs under ASPNETCORE_ENVIRONMENT=Development).
internal static class TestAuth
{
    private static readonly JwtOptions JwtSettings = new()
    {
        Issuer = "MobileBillManagement.Dev",
        Audience = "MobileBillManagement.Dev",
        SigningKey = "dev-only-signing-key-not-for-production-use-please-change-1234567890",
        AccessTokenLifetimeMinutes = 480
    };

    public static string TokenFor(UserRole role, string displayName = "Test User", Guid? userId = null)
    {
        var user = new User { Email = $"{role}@test.local", DisplayName = displayName, Role = role, PasswordHash = "x" };
        if (userId is { } id) user.Id = id;
        var tokenService = new JwtTokenService(Options.Create(JwtSettings));
        return tokenService.CreateAccessToken(user).Token;
    }

    public static void Authorize(HttpClient client, UserRole role, string displayName = "Test User", Guid? userId = null) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(role, displayName, userId));
}
