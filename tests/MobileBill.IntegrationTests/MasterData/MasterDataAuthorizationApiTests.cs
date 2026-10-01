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
        { "POST", "/api/mobile-accounts", new { mobileNumber = "0771234567", employeeEpf = "1", monthlyCreditLimit = 1m, monthlyRental = 1m } },
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

    private static Task<HttpResponseMessage> Send(HttpClient client, string method, string url, object? body)
    {
        var message = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is not null) message.Content = JsonContent.Create(body);
        return client.SendAsync(message);
    }
}
