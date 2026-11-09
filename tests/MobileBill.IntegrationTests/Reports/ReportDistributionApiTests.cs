using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Application.Reports;
using MobileBill.Domain.Enums;

namespace MobileBill.IntegrationTests.Reports;

public sealed class ReportDistributionApiTests
{
    [Theory]
    [InlineData(UserRole.ITEngineer, HttpStatusCode.OK)]
    [InlineData(UserRole.Administrator, HttpStatusCode.OK)]
    [InlineData(UserRole.Cfo, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.GroupHrManager, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Viewer, HttpStatusCode.Forbidden)]
    public async Task Only_IT_engineers_and_administrators_manage_and_send_report_emails(UserRole role, HttpStatusCode expected)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IReportDistributionService>();
            services.AddSingleton<IReportDistributionService>(new FakeService());
        }));
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, role);

        Assert.Equal(expected, (await client.GetAsync("/api/report-distribution/rules")).StatusCode);
        Assert.Equal(expected, (await client.GetAsync($"/api/report-distribution/batches/{Guid.NewGuid()}/preview")).StatusCode);
        Assert.Equal(expected == HttpStatusCode.OK ? HttpStatusCode.OK : expected,
            (await client.PostAsync($"/api/report-distribution/batches/{Guid.NewGuid()}/send", new StringContent("""{"ruleIds":[]}""", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Fact]
    public async Task Delivery_status_is_returned_as_text()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IReportDistributionService>();
            services.AddSingleton<IReportDistributionService>(new FakeService());
        }));
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.ITEngineer);

        var body = await client.GetStringAsync($"/api/report-distribution/batches/{Guid.NewGuid()}/deliveries");

        Assert.Contains("\"status\":\"Failed\"", body);
        Assert.Contains("\"lastError\":\"Refused\"", body);
    }

    [Fact]
    public async Task Email_history_reads_its_filters_from_the_query_string()
    {
        var service = new FakeService();
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IReportDistributionService>();
            services.AddSingleton<IReportDistributionService>(service);
        }));
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.Administrator);
        var batchId = Guid.NewGuid();

        var body = await client.GetStringAsync($"/api/report-distribution/deliveries?batchId={batchId}&status=Failed&search=hr%40concord&pageNumber=2&pageSize=10");

        Assert.Equal(new ReportEmailHistoryRequest(batchId, ReportEmailStatus.Failed, "hr@concord", 2, 10), service.HistoryRequest);
        Assert.Contains("\"totalCount\":11", body);
        Assert.Contains("\"billingMonth\":10", body);
        Assert.Contains("\"requestedBy\":\"Automatic on lock (CFO)\"", body);
    }

    private sealed class FakeService : IReportDistributionService
    {
        public Task<IReadOnlyList<ReportDistributionRuleDto>> ListRulesAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReportDistributionRuleDto>>([]);
        public Task<IReadOnlyList<ReportRecipientOptionDto>> GetRecipientOptionsAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReportRecipientOptionDto>>([]);
        public Task<ReportDistributionRuleDto> CreateRuleAsync(SaveReportDistributionRuleRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReportDistributionRuleDto> UpdateRuleAsync(Guid id, SaveReportDistributionRuleRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteRuleAsync(Guid id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<ReportEmailPreviewDto>> PreviewAsync(Guid batchId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReportEmailPreviewDto>>([]);
        public Task<IReadOnlyList<ReportEmailDeliveryDto>> SendAsync(Guid batchId, SendReportEmailsRequest request, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ReportEmailDeliveryDto>>([]);
        public Task<IReadOnlyList<ReportEmailDeliveryDto>> ListDeliveriesAsync(Guid batchId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReportEmailDeliveryDto>>([new ReportEmailDeliveryDto(Guid.NewGuid(), null, "Rule", "a@concord.lk", ReportEmailStatus.Failed, 5, DateTimeOffset.UnixEpoch, "IT", null, "Refused")]);
        public Task<ReportEmailDeliveryDto> ResendAsync(Guid deliveryId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ReportEmailHistoryRequest? HistoryRequest { get; private set; }
        public Task<MobileBill.Application.Common.PagedResult<ReportEmailDeliveryDto>> SearchDeliveriesAsync(ReportEmailHistoryRequest request, CancellationToken cancellationToken)
        {
            HistoryRequest = request;
            return Task.FromResult(new MobileBill.Application.Common.PagedResult<ReportEmailDeliveryDto>([new ReportEmailDeliveryDto(Guid.NewGuid(), null, "Rule", "a@concord.lk", ReportEmailStatus.Sent, 1, DateTimeOffset.UnixEpoch, "Automatic on lock (CFO)", DateTimeOffset.UnixEpoch, null, Guid.Empty, 2026, 10)], 2, 10, 11));
        }
    }
}
