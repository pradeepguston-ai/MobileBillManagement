using MobileBill.Infrastructure.Identity;

namespace MobileBill.Api.Identity;

// A user signed in with a one-time password may only read their own account and change the password; every other
// API call is refused until they have chosen a new password (which issues a token without this restriction).
public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    private static readonly PathString[] AllowedPaths = ["/api/auth/me", "/api/auth/change-password", "/api/auth/login"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.HasClaim(PasswordChangeClaim.Type, PasswordChangeClaim.Required)
            && context.Request.Path.StartsWithSegments("/api")
            && !AllowedPaths.Any(path => context.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await Results.Problem(
                title: "Password change required",
                detail: "Choose a new password to replace your one-time password before continuing.",
                statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(context);
            return;
        }
        await next(context);
    }
}
