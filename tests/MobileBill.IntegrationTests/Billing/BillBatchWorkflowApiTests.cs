using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MobileBill.Application.Billing;
using MobileBill.Domain.Enums;
using MobileBill.IntegrationTests;

namespace MobileBill.IntegrationTests.Billing;

public sealed class BillBatchWorkflowApiTests
{
    [Fact]
    public async Task Workflow_decision_endpoint_accepts_only_action_and_comment_business_inputs()
    {
        var batchId = Guid.NewGuid();
        var service = new FakeWorkflowService(batchId);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBillApprovalWorkflowService>();
            services.AddSingleton<IBillApprovalWorkflowService>(service);
        }));
        using var client = factory.CreateClient();
        TestAuth.Authorize(client, UserRole.Cfo);

        var response = await client.PostAsJsonAsync($"/api/bill-batches/{batchId}/workflow/decision", new { action = "Approve", comment = "Reviewed", workflowRole = "FinanceApprover", userId = "spoofed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(service.Decision);
        Assert.Equal(ApprovalAction.Approve, service.Decision!.Action);
        Assert.Equal("Reviewed", service.Decision.Comment);
    }

    private sealed class FakeWorkflowService(Guid expectedId) : IBillApprovalWorkflowService
    {
        public Task<BillWorkflowCapabilitiesDto> GetCapabilitiesAsync(Guid batchId, CancellationToken cancellationToken) => Task.FromResult(new BillWorkflowCapabilitiesDto(false, false, false, false, false, "Validation", BillBatchStatus.Validated));
        public WorkflowDecisionRequest? Decision { get; private set; }
        public Task<BillWorkflowResult> SubmitAsync(Guid batchId, WorkflowCommentRequest request, CancellationToken cancellationToken) => Task.FromResult(new BillWorkflowResult(batchId, BillBatchStatus.ITReview));
        public Task<BillWorkflowResult> DecideAsync(Guid batchId, WorkflowDecisionRequest request, CancellationToken cancellationToken) { Assert.Equal(expectedId, batchId); Decision = request; return Task.FromResult(new BillWorkflowResult(batchId, BillBatchStatus.HRApproval)); }
        public Task<BillWorkflowResult> LockAsync(Guid batchId, CancellationToken cancellationToken) => Task.FromResult(new BillWorkflowResult(batchId, BillBatchStatus.Locked));
    }
}
