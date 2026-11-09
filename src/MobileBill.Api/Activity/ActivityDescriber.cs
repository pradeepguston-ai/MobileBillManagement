using MobileBill.Application.Activity;
using MobileBill.Application.Billing;
using MobileBill.Application.Identity;
using MobileBill.Application.MasterData;
using MobileBill.Application.Reports;
using MobileBill.Domain.Enums;

namespace MobileBill.Api.Activity;

// How one successful request reads in the Activity Log. Format holds "{0}" where the target's name goes (a batch's
// billing period, a bill's mobile number, an employee, ...); Fallback is used when it cannot be found.
public sealed record ActivityPlan(string Area, string Format, ActivityTarget? Target = null, Guid? TargetId = null, string? Label = null, string Fallback = "")
{
    public string Describe(string? label) => string.Format(System.Globalization.CultureInfo.InvariantCulture, Format, label ?? Label ?? Fallback).Replace("  ", " ").Trim();
}

// What the request did, by controller and action. Only changes, sign-ins, report downloads and report emails are recorded;
// page views are not. Nothing private goes in (no passwords, no assistant questions).
public static class ActivityDescriber
{
    private static readonly Dictionary<string, (string Area, string Noun, ActivityTarget? Target)> Masters = new()
    {
        ["Employees"] = ("Employees", "employee", ActivityTarget.Employee),
        ["MobileAccounts"] = ("Mobile Allocations", "mobile allocation", ActivityTarget.MobileAccount),
        ["MobileDevices"] = ("Mobile Devices", "mobile device", ActivityTarget.MobileDevice),
        ["Factories"] = ("Masters", "factory", ActivityTarget.Factory),
        ["Departments"] = ("Masters", "department", ActivityTarget.Department),
        ["Sections"] = ("Masters", "section", ActivityTarget.Section),
        ["SubSections"] = ("Masters", "sub section", ActivityTarget.SubSection),
        ["Designations"] = ("Masters", "designation", ActivityTarget.Designation),
        ["Categories"] = ("Masters", "category", ActivityTarget.Category),
        ["MobilePackages"] = ("Masters", "mobile package", ActivityTarget.MobilePackage),
        ["ThirdPartyCompanies"] = ("Masters", "third-party company", ActivityTarget.ThirdPartyCompany),
        ["Providers"] = ("Masters", "telecom provider", ActivityTarget.Provider),
    };

    public static ActivityPlan? Plan(string controller, string action, string method, IDictionary<string, object?> routeValues, IDictionary<string, object?> arguments, object? result, string? downloadedFile)
    {
        var id = RouteId(routeValues, "id") ?? RouteId(routeValues, "batchId");
        if (downloadedFile is not null) return Download(controller, action, downloadedFile);
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method)) return null;

        switch (controller, action)
        {
            case ("Auth", "Login"): return new("Sign-in", "Signed in");
            case ("Auth", _): return null;   // registering and requesting a password reset happen before signing in
            case ("Assistant", "Ask"): return new("AI Assistant", "Asked the AI assistant a question");

            case ("BillBatches", "Create"): return new("Billing", "Created the {0} bill batch", ActivityTarget.BillBatch, Property<Guid>(result, "Id"));
            case ("BillBatches", "Upload"): return new("Billing", "Uploaded the {0} bill PDF", ActivityTarget.BillBatch, id);
            case ("BillBatches", "Parse"): return new("Billing", "Read the {0} bill PDF", ActivityTarget.BillBatch, id);
            case ("BillBatches", "Validate"): return new("Billing", "Validated the {0} bill batch", ActivityTarget.BillBatch, id);
            case ("BillMatching", "Match"): return new("Billing", "Matched the {0} bill numbers to employees", ActivityTarget.BillBatch, id);
            case ("BillMatching", "ResolveMobileAccount"): return new("Billing", "Assigned exception number {0} to an employee", ActivityTarget.BillException, id);
            case ("MonthlyBills", "Assess"):
                return new("Billing", $"Assessed {{0}}{Responsibility(Property<Responsibility?>(result, "Responsibility"))}", ActivityTarget.MonthlyBill, id);
            case ("MonthlyBills", "BulkAssess"):
                return result is BulkAssessmentResultDto bulk
                    ? new("Billing", $"Assessed {bulk.SuccessCount} {(bulk.SuccessCount == 1 ? "number" : "numbers")} in bulk{Responsibility(ArgumentProperty<Responsibility?>(arguments, "Responsibility"))}")
                    : new("Billing", "Assessed numbers in bulk");

            case ("BillBatchWorkflow", "Submit"): return new("Approvals", "Submitted the {0} bill batch for IT review", ActivityTarget.BillBatch, id);
            case ("BillBatchWorkflow", "Decide"): return Decision(arguments, result, id);
            case ("BillBatchWorkflow", "Lock"): return new("Approvals", "Locked the {0} billing period", ActivityTarget.BillBatch, id);

            case ("ReportDistribution", "Create"): return new("Report Emails", "Created report distribution rule {0}", Label: Property<string>(result, "Name"));
            case ("ReportDistribution", "Update"): return new("Report Emails", "Updated report distribution rule {0}", ActivityTarget.ReportRule, id);
            case ("ReportDistribution", "Delete"): return new("Report Emails", "Deleted report distribution rule {0}", ActivityTarget.ReportRule, id);
            case ("ReportDistribution", "Send"):
                var count = result is IReadOnlyCollection<ReportEmailDeliveryDto> sent ? sent.Count : 0;
                return new("Report Emails", $"Emailed the {{0}} report to {count} distribution {(count == 1 ? "rule" : "rules")}", ActivityTarget.BillBatch, id);
            case ("ReportDistribution", "Resend"): return new("Report Emails", "Resent the {0}", ActivityTarget.ReportEmail, id, Fallback: "report email");

            case ("Users", "ChangeRole"): return new("Users", $"Changed the role of {{0}} to {RoleName(Property<UserRole?>(result, "Role"))}", ActivityTarget.User, id);
            case ("Users", "Activate"): return new("Users", "Activated the account of {0}", ActivityTarget.User, id);
            case ("Users", "Deactivate"): return new("Users", "Deactivated the account of {0}", ActivityTarget.User, id);
            case ("Users", "ApprovePasswordReset"): return new("Users", "Approved the password reset of {0}", ActivityTarget.PasswordResetRequest, id);
            case ("Users", "RejectPasswordReset"): return new("Users", "Rejected the password reset of {0}", ActivityTarget.PasswordResetRequest, id);
        }

        return Masters.TryGetValue(controller, out var master) ? MasterChange(master, action, id, result) : null;
    }

    private static ActivityPlan? MasterChange((string Area, string Noun, ActivityTarget? Target) master, string action, Guid? id, object? result)
    {
        // The name from the saved record ("Name (Code)", a mobile number or an employee), else looked up by id.
        var label = Label(result);
        var target = label is null ? master.Target : null;
        var targetId = Property<Guid?>(result, "Id") ?? id;
        ActivityPlan Make(string format) => new(master.Area, format, target, targetId, label);
        return action switch
        {
            "Create" => Make($"Created {master.Noun} {{0}}"),
            "Update" => Make($"Updated {master.Noun} {{0}}"),
            "Deactivate" => Make($"Deactivated {master.Noun} {{0}}") with { Fallback = "" },
            // Checking a file (commit=false) changes nothing.
            "Import" => result is MasterDataImportResult { Committed: true } import
                ? new(master.Area, $"Imported {master.Noun} records from Excel ({import.NewCount} new, {import.UpdatedCount} updated)")
                : null,
            "Resign" => new(master.Area, "Resigned employee {0}", ActivityTarget.Employee, id),
            "CancelResignation" => new(master.Area, "Cancelled the resignation of employee {0}", ActivityTarget.Employee, id),
            "ReleaseToPool" => Make("Released {0} to the SIM Pool"),
            "AssignFromPool" => Make("Assigned {0} from the SIM Pool"),
            "Disconnect" => Make("Disconnected {0}"),
            "Reassign" => Make("Reassigned {0}"),
            "ApplyRental" => new(master.Area, "Applied a mobile package's rental to its allocations"),
            "Issue" => Make("Issued mobile device {0}"),
            "Return" => Make("Recorded the return of mobile device {0}"),
            "Replace" => Make("Replaced mobile device {0}"),
            "Lost" => Make("Marked mobile device {0} as lost"),
            "Repaired" => Make("Marked mobile device {0} as repaired"),
            "Retire" => Make("Retired mobile device {0}"),
            _ => Make($"{Humanize(action)} {master.Noun} {{0}}"),
        };
    }

    private static ActivityPlan Decision(IDictionary<string, object?> arguments, object? result, Guid? batchId)
    {
        var action = ArgumentProperty<ApprovalAction?>(arguments, "Action");
        // An approval moves the batch on, so the new status names the step that was approved.
        var step = result is BillWorkflowResult { Status: var status } ? status switch
        {
            BillBatchStatus.HRApproval => " (IT approval)",
            BillBatchStatus.FinanceApproval => " (HR approval)",
            BillBatchStatus.Completed => " (Finance approval)",
            _ => string.Empty,
        } : string.Empty;
        var format = action switch
        {
            ApprovalAction.Approve => $"Approved the {{0}} bill batch{step}",
            ApprovalAction.Reject => "Rejected the {0} bill batch",
            ApprovalAction.ReturnForCorrection => "Returned the {0} bill batch for correction",
            _ => "Updated the approval of the {0} bill batch",
        };
        return new("Approvals", format, ActivityTarget.BillBatch, batchId);
    }

    private static ActivityPlan? Download(string controller, string action, string fileName)
    {
        if (action.Contains("Template", StringComparison.Ordinal)) return null;   // blank import templates
        var area = controller switch
        {
            "Reports" or "VasReports" => "Reports",
            "BillBatches" => "Billing",
            "MobileAccounts" => "Mobile Allocations",
            "MobileDevices" => "Mobile Devices",
            _ => "Downloads",
        };
        return new(area, "Downloaded {0}", Label: fileName);
    }

    private static string? Label(object? result)
    {
        if (result is null) return null;
        if (Property<string>(result, "MobileNumber") is { Length: > 0 } mobile) return mobile;
        if (Property<string>(result, "AssetTag") is { Length: > 0 } asset) return asset;
        if (Property<string>(result, "Epf") is { Length: > 0 } epf && Property<string>(result, "FullName") is { Length: > 0 } fullName) return $"{epf} {fullName}";
        var code = Property<string>(result, "Code");
        var name = Property<string>(result, "Name") ?? Property<string>(result, "Description");
        return (code, name) switch { ({ Length: > 0 }, { Length: > 0 }) => $"{name} ({code})", ({ Length: > 0 }, _) => code, (_, { Length: > 0 }) => name, _ => null };
    }

    private static string Responsibility(Responsibility? responsibility) => responsibility switch
    {
        Domain.Enums.Responsibility.ByUser => " as By User",
        Domain.Enums.Responsibility.ByCompany => " as By Company",
        _ => string.Empty,
    };

    private static string RoleName(UserRole? role) => role switch
    {
        UserRole.ITEngineer => "IT Engineer", UserRole.HeadOfIt => "Head of IT", UserRole.GroupHrManager => "Group HR Manager", UserRole.Cfo => "CFO",
        UserRole.HrUser => "HR User", UserRole.FinanceUser => "Finance User", { } other => other.ToString(), null => "another role",
    };

    private static string Humanize(string action) =>
        string.Concat(action.Select((letter, index) => index > 0 && char.IsUpper(letter) ? " " + char.ToLowerInvariant(letter) : letter.ToString()));

    private static Guid? RouteId(IDictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out var value) && Guid.TryParse(value?.ToString(), out var id) ? id : null;

    private static T? ArgumentProperty<T>(IDictionary<string, object?> arguments, string property) =>
        arguments.Values.Select(value => Property<T>(value, property)).FirstOrDefault(value => value is not null);

    public static T? Property<T>(object? source, string property)
    {
        if (source is null) return default;
        var value = source.GetType().GetProperty(property)?.GetValue(source);
        return value is T typed ? typed : default;
    }
}
