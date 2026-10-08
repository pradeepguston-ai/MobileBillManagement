namespace MobileBill.Api.Configuration;

public static class RateLimitPolicies
{
    // Anonymous sign-in, registration and password-reset requests, limited per client IP address.
    public const string Authentication = "Authentication";

    // Questions to the AI assistant, limited per signed-in user so one person cannot use up the free model's quota.
    public const string Assistant = "Assistant";
}
