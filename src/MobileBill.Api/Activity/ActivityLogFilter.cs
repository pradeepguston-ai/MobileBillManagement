using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using MobileBill.Application.Activity;
using MobileBill.Application.Identity;

namespace MobileBill.Api.Activity;

// Records each successful change, sign-in, report download and report email in the Activity Log, with who did it.
// Runs for every controller action; a failure to record is logged and never affects the request itself.
// ActivityLog:Enabled = false turns it off (the test hosts do).
public sealed class ActivityLogFilter(IActivityLogService activityLog, IConfiguration configuration, ILogger<ActivityLogFilter> logger) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var enabled = configuration.GetValue("ActivityLog:Enabled", true) && context.ActionDescriptor is ControllerActionDescriptor;
        // A deleted record cannot be named afterwards, so its name is read first.
        string? labelBeforeDelete = null;
        if (enabled && HttpMethods.IsDelete(context.HttpContext.Request.Method))
            labelBeforeDelete = await LabelAsync(Plan(context, result: null, file: null), context.HttpContext.RequestAborted);

        var executed = await next();
        if (!enabled || executed.Exception is not null && !executed.ExceptionHandled || !Succeeded(executed.Result)) return;

        try
        {
            var file = executed.Result as FileResult;
            var value = (executed.Result as ObjectResult)?.Value;
            if (Plan(context, value, file?.FileDownloadName) is not { } plan) return;
            var (userId, userName, role) = WhoActed(context.HttpContext.User, value);
            if (userId is null) return;
            var label = labelBeforeDelete ?? await LabelAsync(plan, CancellationToken.None);
            await activityLog.RecordAsync(new ActivityRecord(userId, userName, role, plan.Area, plan.Describe(label), plan.TargetId?.ToString(),
                context.HttpContext.Request.Method, context.HttpContext.Request.Path.Value ?? string.Empty), CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The activity for {Method} {Path} could not be recorded.", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
        }
    }

    private static ActivityPlan? Plan(ActionExecutingContext context, object? result, string? file)
    {
        var descriptor = (ControllerActionDescriptor)context.ActionDescriptor;
        return ActivityDescriber.Plan(descriptor.ControllerName, descriptor.ActionName, context.HttpContext.Request.Method,
            context.RouteData.Values.ToDictionary(pair => pair.Key, pair => pair.Value), context.ActionArguments, result, file);
    }

    private async Task<string?> LabelAsync(ActivityPlan? plan, CancellationToken cancellationToken) =>
        plan is { Label: null, Target: { } target, TargetId: { } id } ? await activityLog.DescribeTargetAsync(target, id, cancellationToken) : null;

    private static bool Succeeded(IActionResult? result) => result switch
    {
        ObjectResult objectResult => (objectResult.StatusCode ?? StatusCodes.Status200OK) < 400,
        IStatusCodeActionResult statusResult => (statusResult.StatusCode ?? StatusCodes.Status200OK) < 400,
        _ => result is not null,
    };

    // The signed-in user; for a sign-in, the user it signed in.
    private static (string? UserId, string UserName, string Role) WhoActed(ClaimsPrincipal principal, object? result)
    {
        if (result is AuthResultDto signedIn) return (signedIn.User.Id.ToString(), signedIn.User.DisplayName, signedIn.User.Role.ToString());
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        var name = principal.FindFirstValue(ClaimTypes.Name) ?? principal.FindFirstValue("name") ?? principal.FindFirstValue(ClaimTypes.Email) ?? "Unknown user";
        var role = principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        return (id, name, role);
    }
}
