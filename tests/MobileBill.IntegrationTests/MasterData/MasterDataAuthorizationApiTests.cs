using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MobileBill.Domain.Enums;

namespace MobileBill.IntegrationTests.MasterData;

public sealed class MasterDataAuthorizationApiTests
{
    public static TheoryData<string, string, object?> WriteRequests => new()
    {
        { "POST", "/api/departments", new { code = "X", name = "X" } },
        { "PUT", "/api/factories/00000000-0000-0000-0000-000000000001", new { code = "X", name = "X" } },
        { "POST", "/api/designations/00000000-0000-0000-0000-000000000001/deactivate", null },
        { "POST", "/api/employees", new { epf = "1", fullName = "A", categoryCode = "C", designationCode = "D", factoryCode = "F", departmentCode = "P" } },
        { "POST", "/api/mobile-accounts", new { mobileNumber = "0771234567", employeeId = Guid.NewGuid(), monthlyCreditLimit = 1m, monthlyRental = 1m } },
    };

    [Theory]
    [MemberData(nameof(WriteRequests))]
    public async Task Anonymous_users_cannot_change_master_data(string method, string url, object? body)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(client, method, url, body)).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.HeadOfIt)]
    [InlineData(UserRole.GroupHrManager)]
    [InlineData(UserRole.Cfo)]
    public async Task Read_only_roles_cannot_change_any_master_data(UserRole role)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, role);

        foreach (var request in WriteRequests)
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, (string)request[0]!, (string)request[1]!, request[2])).StatusCode);
    }

    public static TheoryData<string, string, object?> AdministratorOnlyWrites => new()
    {
        { "POST", "/api/providers", new { code = "X", name = "X" } },
        { "PUT", "/api/providers/00000000-0000-0000-0000-000000000001", new { code = "X", name = "X" } },
        { "POST", "/api/mobile-accounts", new { mobileNumber = "0771234567", employeeId = Guid.NewGuid(), monthlyCreditLimit = 1m, monthlyRental = 1m } },
        { "PUT", "/api/mobile-accounts/00000000-0000-0000-0000-000000000001", new { mobileNumber = "0771234567", employeeId = Guid.NewGuid(), monthlyCreditLimit = 1m, monthlyRental = 1m } },
        { "POST", "/api/mobile-accounts/00000000-0000-0000-0000-000000000001/deactivate", null },
        { "POST", "/api/mobile-accounts/00000000-0000-0000-0000-000000000001/reassign", new { employeeId = Guid.NewGuid() } },
        { "POST", "/api/mobile-accounts/00000000-0000-0000-0000-000000000001/release-to-pool", new { resignedOn = "2026-10-01", reason = "Resigned" } },
        { "POST", "/api/mobile-accounts/00000000-0000-0000-0000-000000000001/assign-from-pool", new { employeeId = Guid.NewGuid() } },
        { "POST", "/api/mobile-accounts/00000000-0000-0000-0000-000000000001/disconnect", new { disconnectedOn = "2026-10-01" } },
        { "POST", "/api/mobile-packages", new { code = "X", providerId = Guid.NewGuid(), description = "X", monthlyRental = 1m, totalWithTax = 1m, defaultCreditLimit = 1m } },
        { "POST", "/api/mobile-devices", new { assetTag = "X", imei1 = "356789010000014", brand = "X", model = "X", purchaseDate = "2026-01-01", purchaseCost = 1m } },
        { "POST", "/api/mobile-devices/00000000-0000-0000-0000-000000000001/issue", new { employeeId = Guid.NewGuid(), issuedOn = "2026-01-01" } },
        { "POST", "/api/mobile-devices/00000000-0000-0000-0000-000000000001/return", new { returnedOn = "2026-01-01", condition = "InStock", reason = "Other" } },
        { "POST", "/api/mobile-devices/00000000-0000-0000-0000-000000000001/lost", new { lostOn = "2026-01-01" } },
        { "POST", "/api/bill-batches", new { providerId = Guid.NewGuid(), corporateCode = "C", billingYear = 2026, billingMonth = 9 } },
    };

    // An empty code fails validation before any database work, so a non-403 answer shows the role was let through.
    public static TheoryData<string, string, object?> OrganisationMasterWrites => new()
    {
        { "POST", "/api/employees", new { epf = "", fullName = "", categoryCode = "C", designationCode = "D", factoryCode = "F", departmentCode = "P" } },
        { "POST", "/api/factories", new { code = "", name = "" } },
        { "POST", "/api/departments", new { code = "", name = "" } },
        { "POST", "/api/sections", new { code = "", name = "", departmentCode = "" } },
        { "POST", "/api/sub-sections", new { code = "", name = "", sectionCode = "" } },
        { "POST", "/api/designations", new { code = "", name = "" } },
        { "POST", "/api/categories", new { code = "", name = "" } },
    };

    public static TheoryData<string, object?> SimPoolActions => new()
    {
        { "/api/mobile-accounts/00000000-0000-0000-0000-000000000001/release-to-pool", new { resignedOn = "2026-10-01", reason = "Resigned" } },
        { "/api/mobile-accounts/00000000-0000-0000-0000-000000000001/assign-from-pool", new { employeeId = Guid.NewGuid() } },
        { "/api/mobile-accounts/00000000-0000-0000-0000-000000000001/disconnect", new { disconnectedOn = "2026-10-01" } },
        { "/api/employees/00000000-0000-0000-0000-000000000001/resign", new { resignedOn = "2026-10-01" } },
    };

    [Theory]
    [MemberData(nameof(SimPoolActions))]
    public async Task Head_of_it_cannot_change_the_sim_pool(string url, object? body)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.HeadOfIt);

        Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, "POST", url, body)).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.HrUser)]
    [InlineData(UserRole.FinanceUser)]
    public async Task Hr_and_finance_users_cannot_change_providers_packages_allocations_or_billing(UserRole role)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, role);

        foreach (var request in AdministratorOnlyWrites)
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(client, (string)request[0]!, (string)request[1]!, request[2])).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.HrUser)]
    [InlineData(UserRole.FinanceUser)]
    public async Task Hr_and_finance_users_may_maintain_organisation_masters(UserRole role)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, role);

        foreach (var request in OrganisationMasterWrites)
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, (string)request[0]!, (string)request[1]!, request[2])).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.HeadOfIt)]
    [InlineData(UserRole.Cfo)]
    public async Task Other_read_only_roles_cannot_reassign_mobile_numbers(UserRole role)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, role);

        var response = await Send(client, "POST", "/api/mobile-accounts/00000000-0000-0000-0000-000000000001/reassign", new { employeeId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.HrUser, "/api/employees/import/template", HttpStatusCode.OK)]
    [InlineData(UserRole.HrUser, "/api/mobile-accounts/import/template", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.ITEngineer, "/api/mobile-accounts/import/template", HttpStatusCode.OK)]
    [InlineData(UserRole.HeadOfIt, "/api/employees/import/template", HttpStatusCode.Forbidden)]
    public async Task Import_templates_follow_the_master_data_edit_rights(UserRole role, string url, HttpStatusCode expected)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, role);

        var response = await client.GetAsync(url);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK) Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData(UserRole.HrUser, "/api/mobile-accounts/import", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Cfo, "/api/employees/import", HttpStatusCode.Forbidden)]
    [InlineData(UserRole.HrUser, "/api/employees/import", HttpStatusCode.BadRequest)]   // allowed; a non-Excel file is rejected before any database work
    public async Task Import_uploads_follow_the_master_data_edit_rights(UserRole role, string url, HttpStatusCode expected)
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, role);
        using var form = new MultipartFormDataContent { { new ByteArrayContent("not excel"u8.ToArray()), "file", "employees.csv" } };

        var response = await client.PostAsync(url, form);

        Assert.Equal(expected, response.StatusCode);
    }

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string url, object? body)
    {
        var message = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is not null) message.Content = JsonContent.Create(body);
        return client.SendAsync(message);
    }
}
