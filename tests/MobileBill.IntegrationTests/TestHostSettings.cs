using System.Runtime.CompilerServices;

namespace MobileBill.IntegrationTests;

internal static class TestHostSettings
{
    // TestAuth signs tokens for users that are not in the test databases, so the per-request account check is
    // switched off for the test hosts. UserSessionValidationApiTests switches it back on to cover that check.
    [ModuleInitializer]
    internal static void Initialize() =>
        Environment.SetEnvironmentVariable("Jwt__ValidateUserOnEachRequest", "false");
}
