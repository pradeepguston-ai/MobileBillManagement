using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Application.Billing;
using MobileBill.Domain.Enums;
using MobileBill.IntegrationTests;

namespace MobileBill.IntegrationTests.Billing;

public sealed class MonthlyBillAssessmentApiTests
{
    [Fact]
    public async Task Assessment_endpoint_accepts_only_business_inputs_and_returns_server_owned_assessment_metadata()
    {
        var id = Guid.NewGuid();
        var service = new FakeAssessmentService(id);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBillAssessmentService>();
            services.AddSingleton<IBillAssessmentService>(service);
        }));
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.ITEngineer);

        var response = await client.PutAsJsonAsync($"/api/monthly-bills/{id}/assessment", new { responsibility = "ByUser", finalDeduction = 12.50m, reason = "Approved adjustment", assessedBy = "spoofed", assessedAt = "2000-01-01T00:00:00Z", overrideBy = "spoofed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(service.Request);
        Assert.Equal(Responsibility.ByUser, service.Request!.Responsibility);
        Assert.Equal(12.50m, service.Request.FinalDeduction);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        var result = await response.Content.ReadFromJsonAsync<MonthlyBillAssessmentDto>(jsonOptions);
        Assert.NotNull(result);
        Assert.Equal("dev-user", result.AssessedBy);
        Assert.Equal(DateTimeOffset.UnixEpoch, result.AssessedAt);
    }

    [Fact]
    public async Task Bulk_assessment_endpoint_requires_authorization_and_returns_per_item_results()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var service = new FakeAssessmentService(ids[0]);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBillAssessmentService>();
            services.AddSingleton<IBillAssessmentService>(service);
        }));

        using var anonymousClient = factory.CreateClient();
        var unauthorized = await anonymousClient.PostAsJsonAsync("/api/monthly-bills/bulk-assessment", new { monthlyBillIds = ids, responsibility = "ByCompany", reason = (string?)null });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.ITEngineer);
        var response = await client.PostAsJsonAsync("/api/monthly-bills/bulk-assessment", new { monthlyBillIds = ids, responsibility = "ByCompany", reason = (string?)null });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BulkAssessmentResultDto>();
        Assert.NotNull(result);
        Assert.Equal(1, result!.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Contains(result.Items, item => item.MonthlyBillId == ids[0] && item.Success);
        Assert.Contains(result.Items, item => item.MonthlyBillId == ids[1] && !item.Success);
    }

    private sealed class FakeAssessmentService(Guid expectedId) : IBillAssessmentService
    {
        public AssessMonthlyBillRequest? Request { get; private set; }
        public Task<MonthlyBillAssessmentDto> AssessAsync(Guid monthlyBillId, AssessMonthlyBillRequest request, CancellationToken cancellationToken)
        {
            Assert.Equal(expectedId, monthlyBillId); Request = request;
            return Task.FromResult(new MonthlyBillAssessmentDto(monthlyBillId, request.Responsibility, 100m, 100m, 0m, 0m, request.FinalDeduction ?? 0m, DateTimeOffset.UnixEpoch, "dev-user", null, null, null, null));
        }

        public Task<BulkAssessmentResultDto> BulkAssessAsync(BulkAssessMonthlyBillsRequest request, CancellationToken cancellationToken)
        {
            var items = request.MonthlyBillIds.Select(id => new BulkAssessmentItemResult(id, id == expectedId, id == expectedId ? null : "not found")).ToList();
            return Task.FromResult(new BulkAssessmentResultDto(items.Count(item => item.Success), items.Count(item => !item.Success), items));
        }
    }
}
