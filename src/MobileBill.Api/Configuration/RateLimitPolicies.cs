namespace MobileBill.Api.Configuration;

public static class RateLimitPolicies
{
    // Anonymous sign-in, registration and password-reset requests, limited per client IP address.
    public const string Authentication = "Authentication";
}
