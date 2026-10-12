using System.Runtime.CompilerServices;

namespace MobileBill.IntegrationTests;

internal static class TestHostSettings
{
    // TestAuth signs tokens for users that are not in the test databases, so the per-request account check is
    // switched off for the test hosts. UserSessionValidationApiTests switches it back on to cover that check.
    // The hourly resignation job is off too: it would run as soon as a host starts, on the same database (one shared
    // SQLite connection in some tests) that the test is still setting up.
    [ModuleInitializer]
    internal static void Initialize()
    {
        Environment.SetEnvironmentVariable("Jwt__ValidateUserOnEachRequest", "false");
        Environment.SetEnvironmentVariable("BackgroundJobs__ResignationCompletion", "false");
    }
}
