namespace MobileBill.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string SigningKey { get; set; }
    public int AccessTokenLifetimeMinutes { get; set; } = 480;

    // Checks on every request that the token's account is still Active and still has the role in the token,
    // so deactivating a user or changing their role takes effect immediately instead of when the token expires.
    public bool ValidateUserOnEachRequest { get; set; } = true;
}
