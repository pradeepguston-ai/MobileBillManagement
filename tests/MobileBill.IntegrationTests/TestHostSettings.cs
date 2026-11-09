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
        // The test hosts load appsettings.Development.json, whose connection string is a real database. Tests swap in their
        // own (in-memory or SQLite) databases; this makes any test that forgets fail at once instead of touching that database.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=tcp:127.0.0.1,1;Database=NoDatabaseInTests;User ID=none;Password=none;Connect Timeout=1;Encrypt=False");
        // Each successful action would add an Activity Log row; ActivityLogApiTests switches this on with its own database.
        Environment.SetEnvironmentVariable("ActivityLog__Enabled", "false");
    }
}
