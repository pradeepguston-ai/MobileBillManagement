using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.IntegrationTests.Identity;

public sealed class PermissionsApiTests
{
    private static readonly string Missing = $"/api/employees/{Guid.Empty}";

    [Fact]
    public async Task Without_saved_permissions_every_role_has_todays_access()
    {
        using var api = new PermissionsApi();
        api.As(UserRole.Administrator);

        var matrix = await api.Client.GetFromJsonAsync<Matrix>("/api/permissions");

        var hr = matrix!.Roles.Single(role => role.Role == "HrUser");
        Assert.False(hr.Customised);
        Assert.Equal((true, true, true, true), Flags(hr, "employees"));
        Assert.Equal((true, false, false, false), Flags(hr, "providers"));
        Assert.Equal((false, false, false, false), Flags(hr, "report-distribution"));
        Assert.True(matrix.Roles.Single(role => role.Role == "Administrator").Locked);
        Assert.Equal(["View"], matrix.Modules.Single(module => module.Key == "dashboard").Actions);
    }

    [Fact]
    public async Task A_saved_permission_is_enforced_and_reset_brings_back_the_default()
    {
        using var api = new PermissionsApi();
        api.As(UserRole.HrUser);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await api.Client.PutAsJsonAsync(Missing, Employee())).StatusCode);

        api.As(UserRole.Administrator);
        var saved = await api.Client.PutAsJsonAsync("/api/permissions/roles/HrUser", new { permissions = new[] { Permission("employees", view: true, create: true) } });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.True((await saved.Content.ReadFromJsonAsync<RoleRow>())!.Customised);

        api.As(UserRole.HrUser);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.PutAsJsonAsync(Missing, Employee())).StatusCode);
        var mine = await api.Client.GetFromJsonAsync<List<Row>>("/api/permissions/mine");
        Assert.Equal(new Row("employees", true, true, false, false), mine!.Single(row => row.Module == "employees"));

        api.As(UserRole.Administrator);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.PostAsync("/api/permissions/roles/HrUser/reset", null)).StatusCode);
        api.As(UserRole.HrUser);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await api.Client.PutAsJsonAsync(Missing, Employee())).StatusCode);
    }

    [Fact]
    public async Task A_role_can_be_given_more_than_its_default_and_lose_a_screen()
    {
        using var api = new PermissionsApi();
        api.As(UserRole.Administrator);
        await api.Client.PutAsJsonAsync("/api/permissions/roles/Viewer", new { permissions = new[] { Permission("providers", create: true), Permission("activity-log", view: false) } });

        api.As(UserRole.Viewer);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await api.Client.PostAsJsonAsync("/api/providers", new { code = "X", name = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.GetAsync("/api/activity-log")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.PutAsJsonAsync("/api/providers/" + Guid.Empty, new { code = "X", name = "X" })).StatusCode);
    }

    [Fact]
    public async Task Saving_turns_on_view_with_any_change_and_ignores_actions_a_screen_does_not_have()
    {
        using var api = new PermissionsApi();
        api.As(UserRole.Administrator);

        var response = await api.Client.PutAsJsonAsync("/api/permissions/roles/Cfo", new { permissions = new[] { Permission("factories", delete: true), Permission("dashboard", view: false, create: true, edit: true) } });

        var saved = await response.Content.ReadFromJsonAsync<RoleRow>();
        Assert.Equal((true, false, false, true), Flags(saved!, "factories"));
        Assert.Equal((false, false, false, false), Flags(saved!, "dashboard"));
    }

    [Fact]
    public async Task The_administrator_role_and_unknown_screens_cannot_be_saved()
    {
        using var api = new PermissionsApi();
        api.As(UserRole.Administrator);

        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client.PutAsJsonAsync("/api/permissions/roles/Administrator", new { permissions = new[] { Permission("employees") } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client.PostAsync("/api/permissions/roles/Administrator/reset", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await api.Client.PutAsJsonAsync("/api/permissions/roles/HrUser", new { permissions = new[] { Permission("no-such-screen") } })).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.ITEngineer)]
    [InlineData(UserRole.HeadOfIt)]
    public async Task Only_administrators_see_and_change_the_permissions(UserRole role)
    {
        using var api = new PermissionsApi();
        api.As(role);

        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.GetAsync("/api/permissions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.PutAsJsonAsync("/api/permissions/roles/Viewer", new { permissions = Array.Empty<object>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.PostAsync("/api/permissions/roles/Viewer/reset", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/permissions/mine")).StatusCode);
    }

    [Fact]
    public async Task User_management_is_for_administrators_until_granted()
    {
        using var api = new PermissionsApi();
        api.As(UserRole.HeadOfIt);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.GetAsync("/api/users/password-resets")).StatusCode);

        api.As(UserRole.Administrator);
        await api.Client.PutAsJsonAsync("/api/permissions/roles/HeadOfIt", new { permissions = new[] { Permission("user-management") } });

        api.As(UserRole.HeadOfIt);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.PostAsJsonAsync("/api/users", NewUser("Viewer"))).StatusCode);
    }

    [Fact]
    public async Task A_granted_role_manages_other_accounts_but_never_administrators_its_own_role_or_permissions()
    {
        using var api = new PermissionsApi();
        var self = await api.SeedUserAsync("head@concord.lk", UserRole.HeadOfIt);
        var admin = await api.SeedUserAsync("admin@concord.lk", UserRole.Administrator);
        var viewer = await api.SeedUserAsync("viewer@concord.lk", UserRole.Viewer);
        api.As(UserRole.Administrator);
        await api.Client.PutAsJsonAsync("/api/permissions/roles/HeadOfIt", new { permissions = new[] { Permission("user-management", create: true, edit: true, delete: true) } });
        api.As(UserRole.HeadOfIt, self);

        Assert.Equal(HttpStatusCode.OK, (await api.Client.PostAsJsonAsync("/api/users", NewUser("HrUser"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.PutAsJsonAsync($"/api/users/{viewer}/role", new { role = "HrUser" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Client.PostAsync($"/api/users/{viewer}/deactivate", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.PostAsJsonAsync("/api/users", NewUser("Administrator", "second@concord.lk"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.PutAsJsonAsync($"/api/users/{viewer}/role", new { role = "Administrator" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.PutAsJsonAsync($"/api/users/{admin}/role", new { role = "Viewer" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.PostAsync($"/api/users/{admin}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.PostAsJsonAsync($"/api/users/{viewer}/activate", new { roleOverride = "Administrator" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.PutAsJsonAsync($"/api/users/{self}/role", new { role = "Cfo" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.GetAsync("/api/permissions")).StatusCode);
    }

    [Fact]
    public async Task A_granted_role_cannot_decide_password_resets_for_administrators_or_itself()
    {
        using var api = new PermissionsApi();
        var self = await api.SeedUserAsync("head@concord.lk", UserRole.HeadOfIt);
        var adminReset = await api.SeedResetAsync(await api.SeedUserAsync("admin@concord.lk", UserRole.Administrator));
        var ownReset = await api.SeedResetAsync(self);
        var viewerReset = await api.SeedResetAsync(await api.SeedUserAsync("viewer@concord.lk", UserRole.Viewer));
        api.As(UserRole.Administrator);
        await api.Client.PutAsJsonAsync("/api/permissions/roles/HeadOfIt", new { permissions = new[] { Permission("user-management", edit: true) } });
        api.As(UserRole.HeadOfIt, self);

        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.PostAsync($"/api/users/password-resets/{adminReset}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Client.PostAsync($"/api/users/password-resets/{ownReset}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await api.Client.PostAsync($"/api/users/password-resets/{viewerReset}/approve", null)).StatusCode);
    }

    private static object NewUser(string role, string email = "new.user@concord.lk") => new { email, displayName = "New User", role, password = "Str0ngPass!" };

    private static object Permission(string module, bool view = true, bool create = false, bool edit = false, bool delete = false) =>
        new { module, canView = view, canCreate = create, canEdit = edit, canDelete = delete };

    private static object Employee() => new { epf = "1", fullName = "A", gender = "Male", categoryCode = "C", designationCode = "D", factoryCode = "F", departmentCode = "P" };

    private static (bool, bool, bool, bool) Flags(RoleRow role, string module)
    {
        var row = role.Permissions.Single(item => item.Module == module);
        return (row.CanView, row.CanCreate, row.CanEdit, row.CanDelete);
    }

    private sealed record Row(string Module, bool CanView, bool CanCreate, bool CanEdit, bool CanDelete);
    private sealed record RoleRow(string Role, bool Locked, bool Customised, List<Row> Permissions);
    private sealed record ModuleRow(string Key, string Label, string Group, string? Note, List<string> Actions);
    private sealed record Matrix(List<ModuleRow> Modules, List<RoleRow> Roles);

    private sealed class PermissionsApi : IDisposable
    {
        private readonly WebApplicationFactory<Program> factory;
        public HttpClient Client { get; }

        public PermissionsApi()
        {
            var databaseName = $"permissions-{Guid.NewGuid():N}";
            factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<MobileBillDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<MobileBillDbContext>>();
                services.RemoveAll<MobileBillDbContext>();
                services.AddDbContext<MobileBillDbContext>(options => options.UseInMemoryDatabase(databaseName));
            }));
            Client = factory.CreateClient();
        }

        public void As(UserRole role, Guid? userId = null) => TestAuth.Authorize(Client, role, userId: userId);

        public async Task<Guid> SeedUserAsync(string email, UserRole role)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var user = new User { Email = email, DisplayName = email, PasswordHash = "hash", Role = role, Status = UserAccountStatus.Active };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        }

        public async Task<Guid> SeedResetAsync(Guid userId)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MobileBillDbContext>();
            var request = new PasswordResetRequest { UserId = userId, NewPasswordHash = "new-hash", Status = PasswordResetStatus.Pending, CreatedAtUtc = DateTimeOffset.UtcNow };
            db.PasswordResetRequests.Add(request);
            await db.SaveChangesAsync();
            return request.Id;
        }

        public void Dispose() { Client.Dispose(); factory.Dispose(); }
    }
}
