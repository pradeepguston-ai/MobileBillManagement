using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Common;
using MobileBill.Application.Reports;

namespace MobileBill.Api.Controllers;

// Emailing the monthly bill report: the distribution rules, and sending a batch's reports. IT Engineer and Administrator only.
[ApiController, Route("api/report-distribution"), Authorize(Roles = MasterDataRoles.Editors)]
public sealed class ReportDistributionController(IReportDistributionService service) : ControllerBase
{
    [HttpGet("rules")] public Task<IReadOnlyList<ReportDistributionRuleDto>> Rules(CancellationToken token) => service.ListRulesAsync(token);
    [HttpGet("recipient-options")] public Task<IReadOnlyList<ReportRecipientOptionDto>> RecipientOptions(CancellationToken token) => service.GetRecipientOptionsAsync(token);
    [HttpPost("rules")] public Task<ReportDistributionRuleDto> Create(SaveReportDistributionRuleRequest request, CancellationToken token) => service.CreateRuleAsync(request, token);
    [HttpPut("rules/{id:guid}")] public Task<ReportDistributionRuleDto> Update(Guid id, SaveReportDistributionRuleRequest request, CancellationToken token) => service.UpdateRuleAsync(id, request, token);
    [HttpDelete("rules/{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken token) { await service.DeleteRuleAsync(id, token); return NoContent(); }

    [HttpGet("batches/{batchId:guid}/preview")] public Task<IReadOnlyList<ReportEmailPreviewDto>> Preview(Guid batchId, CancellationToken token) => service.PreviewAsync(batchId, token);
    [HttpPost("batches/{batchId:guid}/send")] public Task<IReadOnlyList<ReportEmailDeliveryDto>> Send(Guid batchId, SendReportEmailsRequest request, CancellationToken token) => service.SendAsync(batchId, request, token);
    [HttpGet("batches/{batchId:guid}/deliveries")] public Task<IReadOnlyList<ReportEmailDeliveryDto>> Deliveries(Guid batchId, CancellationToken token) => service.ListDeliveriesAsync(batchId, token);
    [HttpGet("deliveries")] public Task<PagedResult<ReportEmailDeliveryDto>> History([FromQuery] ReportEmailHistoryRequest request, CancellationToken token) => service.SearchDeliveriesAsync(request, token);
    [HttpPost("deliveries/{id:guid}/resend")] public Task<ReportEmailDeliveryDto> Resend(Guid id, CancellationToken token) => service.ResendAsync(id, token);
}
