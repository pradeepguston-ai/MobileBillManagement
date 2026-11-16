using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MobileBill.Domain.Entities;

namespace MobileBill.Infrastructure.Identity;

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAtUtc) CreateAccessToken(User user);
}

public static class PasswordChangeClaim
{
    public const string Type = "pwd_change";
    public const string Required = "required";
}

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public (string Token, DateTimeOffset ExpiresAtUtc) CreateAccessToken(User user)
    {
        var settings = options.Value;
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(settings.AccessTokenLifetimeMinutes);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        // A token issued for a one-time password only lets the user change it (PasswordChangeRequiredMiddleware).
        if (user.MustChangePassword) claims = [.. claims, new Claim(PasswordChangeClaim.Type, PasswordChangeClaim.Required)];
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: settings.Issuer,
            audience: settings.Audience,
            claims: claims,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
