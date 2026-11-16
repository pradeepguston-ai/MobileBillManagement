using System.Text.Json.Serialization;
using MobileBill.Domain.Enums;

namespace MobileBill.Application.Identity;

// The screens whose access an administrator can set per role. Keys are stored in RolePermissions.Module and used by the
// frontend, so they must not change. The billing approval chain (prepare, submit, IT / HR / Finance approval, lock) is
// not here: it stays fixed to its roles.
public static class PermissionModules
{
    public const string Dashboard = "dashboard";
    public const string MonthlyBillReview = "monthly-bill-review";
    public const string BillingBatches = "billing-batches";
    public const string ExceptionReview = "exception-review";
    public const string Employees = "employees";
    public const string MobileAllocations = "mobile-allocations";
    public const string MobileDevices = "mobile-devices";
    public const string Factories = "factories";
    public const string Departments = "departments";
    public const string Designations = "designations";
    public const string Categories = "categories";
    public const string MobilePackages = "mobile-packages";
    public const string Providers = "providers";
    public const string ThirdPartyCompanies = "third-party-companies";
    public const string MonthlyBillReport = "monthly-bill-report";
    public const string VasReport = "vas-report";
    public const string ReportDistribution = "report-distribution";
    public const string ActivityLog = "activity-log";
    public const string AiAssistant = "ai-assistant";
    public const string UserManagement = "user-management";
}

// A screen, the actions that apply to it, and which roles have each action by default (today's access rules).
public sealed record PermissionModuleDefinition(string Key, string Label, string Group, string? Note, IReadOnlyList<PermissionAction> Actions, IReadOnlyDictionary<PermissionAction, UserRole[]> Defaults)
{
    public bool Supports(PermissionAction action) => Actions.Contains(action);
    public bool DefaultAllows(UserRole role, PermissionAction action) => Supports(action) && Defaults.TryGetValue(action, out var roles) && roles.Contains(role);
}

public static class PermissionCatalog
{
    private static readonly UserRole[] Everyone = Enum.GetValues<UserRole>();
    // Administrator only (it is always allowed, so no other role has it until an Administrator grants it).
    private static readonly UserRole[] Nobody = [];
    private static readonly UserRole[] ItEditors = [UserRole.Administrator, UserRole.ITEngineer];
    private static readonly UserRole[] OrganisationEditors = [UserRole.Administrator, UserRole.ITEngineer, UserRole.HrUser, UserRole.FinanceUser];
    private static readonly PermissionAction[] ViewOnly = [PermissionAction.View];
    private static readonly PermissionAction[] AllActions = [PermissionAction.View, PermissionAction.Create, PermissionAction.Edit, PermissionAction.Delete];

    private static PermissionModuleDefinition Screen(string key, string label, string group, string? note = null, UserRole[]? viewers = null) =>
        new(key, label, group, note, ViewOnly, new Dictionary<PermissionAction, UserRole[]> { [PermissionAction.View] = viewers ?? Everyone });

    private static PermissionModuleDefinition Master(string key, string label, string group, UserRole[] editors, string? note = null, UserRole[]? viewers = null) =>
        new(key, label, group, note, AllActions, new Dictionary<PermissionAction, UserRole[]>
        {
            [PermissionAction.View] = viewers ?? Everyone, [PermissionAction.Create] = editors, [PermissionAction.Edit] = editors, [PermissionAction.Delete] = editors,
        });

    private const string FixedChain = "Preparing, assessing, submitting and approving follow the fixed approval chain.";

    public static IReadOnlyList<PermissionModuleDefinition> Modules { get; } =
    [
        Screen(PermissionModules.Dashboard, "Dashboard", "General"),
        Screen(PermissionModules.MonthlyBillReview, "Monthly Bill Review", "Billing", FixedChain),
        Screen(PermissionModules.BillingBatches, "Billing Batches", "Billing", FixedChain),
        Screen(PermissionModules.ExceptionReview, "Exception Review", "Billing", FixedChain),
        Master(PermissionModules.Employees, "Employees", "Masters", OrganisationEditors, "Edit includes Excel import (with Create) and resignations."),
        Master(PermissionModules.MobileAllocations, "Mobile Allocations", "Masters", ItEditors, "Edit includes reassigning and the SIM Pool; Delete covers deactivating and disconnecting numbers."),
        Master(PermissionModules.MobileDevices, "Mobile Devices", "Masters", ItEditors, "Edit includes issue, return, replace, lost and repaired; Delete retires a device."),
        Master(PermissionModules.Factories, "Factories", "Masters", OrganisationEditors),
        Master(PermissionModules.Departments, "Departments, Sections & Sub Sections", "Masters", OrganisationEditors),
        Master(PermissionModules.Designations, "Designations", "Masters", OrganisationEditors),
        Master(PermissionModules.Categories, "Categories", "Masters", OrganisationEditors),
        Master(PermissionModules.MobilePackages, "Mobile Packages", "Masters", ItEditors, "Edit includes applying a package's rental to its allocations."),
        Master(PermissionModules.Providers, "Telecom Providers", "Masters", ItEditors),
        Master(PermissionModules.ThirdPartyCompanies, "Third Party Companies", "Masters", OrganisationEditors),
        Screen(PermissionModules.MonthlyBillReport, "Monthly Bill Report", "Reports", "View includes downloading the Excel and PDF reports."),
        Screen(PermissionModules.VasReport, "VAS Report", "Reports"),
        Master(PermissionModules.ReportDistribution, "Report Distribution & Email History", "Reports", ItEditors, "Create, Edit and Delete apply to distribution rules; Edit also sends and resends report emails.", ItEditors),
        Screen(PermissionModules.ActivityLog, "Activity Log", "Audit"),
        Screen(PermissionModules.AiAssistant, "AI Assistant", "General"),
        Master(PermissionModules.UserManagement, "User Management & Password Resets", "Administration", Nobody,
            "Create adds accounts; Edit changes roles, activates accounts and decides password resets; Delete deactivates or rejects. " +
            "Only Administrators can manage Administrator accounts, change their own role or set permissions.", Nobody),
    ];

    private static readonly Dictionary<string, PermissionModuleDefinition> ByKey = Modules.ToDictionary(module => module.Key, StringComparer.Ordinal);

    public static PermissionModuleDefinition? Find(string key) => ByKey.GetValueOrDefault(key);

    // The built-in rule, before anything is saved in User Management. Administrator is always allowed.
    public static bool DefaultAllows(UserRole role, string module, PermissionAction action) =>
        role == UserRole.Administrator ? Find(module)?.Supports(action) == true : Find(module)?.DefaultAllows(role, action) == true;
}

public sealed record ModulePermissionDto(string Module, bool CanView, bool CanCreate, bool CanEdit, bool CanDelete);
// Actions are the PermissionAction names that apply to the screen ("View", "Create", "Edit", "Delete").
public sealed record PermissionModuleDto(string Key, string Label, string Group, string? Note, IReadOnlyList<string> Actions);
// Locked: the Administrator role, which always has full access. Customised: at least one screen differs from the defaults.
public sealed record RolePermissionsDto([property: JsonConverter(typeof(JsonStringEnumConverter))] UserRole Role, bool Locked, bool Customised, IReadOnlyList<ModulePermissionDto> Permissions);
public sealed record PermissionMatrixDto(IReadOnlyList<PermissionModuleDto> Modules, IReadOnlyList<RolePermissionsDto> Roles);
public sealed record SaveRolePermissionsRequest(IReadOnlyList<ModulePermissionDto> Permissions);

public interface IPermissionService
{
    Task<bool> IsAllowedAsync(UserRole role, string module, PermissionAction action, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModulePermissionDto>> GetForRoleAsync(UserRole role, CancellationToken cancellationToken);
    Task<PermissionMatrixDto> GetMatrixAsync(CancellationToken cancellationToken);
    Task<RolePermissionsDto> SaveRoleAsync(UserRole role, SaveRolePermissionsRequest request, CancellationToken cancellationToken);
    Task<RolePermissionsDto> ResetRoleAsync(UserRole role, CancellationToken cancellationToken);
}

public sealed class PermissionValidationException(string message) : Exception(message);
