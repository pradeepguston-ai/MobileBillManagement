using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Domain.Enums;
using MobileBill.Application.Identity;
using MobileBill.Api.Identity;
using MobileBill.Application.Common;
using MobileBill.Application.Reports;

namespace MobileBill.Api.Controllers;

// Emailing the monthly bill report: the distribution rules, and sending a batch's reports. Access follows the Report Distribution permissions in User Management.
[ApiController, Route("api/report-distribution"), Authorize]
public sealed class ReportDistributionController(IReportDistributionService service) : ControllerBase
{
    [HttpGet("rules"), Permission(PermissionModules.ReportDistribution, PermissionAction.View)] public Task<IReadOnlyList<ReportDistributionRuleDto>> Rules(CancellationToken token) => service.ListRulesAsync(token);
    [HttpGet("recipient-options"), Permission(PermissionModules.ReportDistribution, PermissionAction.View)] public Task<IReadOnlyList<ReportRecipientOptionDto>> RecipientOptions(CancellationToken token) => service.GetRecipientOptionsAsync(token);
    [HttpPost("rules"), Permission(PermissionModules.ReportDistribution, PermissionAction.Create)] public Task<ReportDistributionRuleDto> Create(SaveReportDistributionRuleRequest request, CancellationToken token) => service.CreateRuleAsync(request, token);
    [HttpPut("rules/{id:guid}"), Permission(PermissionModules.ReportDistribution, PermissionAction.Edit)] public Task<ReportDistributionRuleDto> Update(Guid id, SaveReportDistributionRuleRequest request, CancellationToken token) => service.UpdateRuleAsync(id, request, token);
    [HttpDelete("rules/{id:guid}"), Permission(PermissionModules.ReportDistribution, PermissionAction.Delete)] public async Task<IActionResult> Delete(Guid id, CancellationToken token) { await service.DeleteRuleAsync(id, token); return NoContent(); }

    [HttpGet("batches/{batchId:guid}/preview"), Permission(PermissionModules.ReportDistribution, PermissionAction.View)] public Task<IReadOnlyList<ReportEmailPreviewDto>> Preview(Guid batchId, CancellationToken token) => service.PreviewAsync(batchId, token);
    [HttpPost("batches/{batchId:guid}/send"), Permission(PermissionModules.ReportDistribution, PermissionAction.Edit)] public Task<IReadOnlyList<ReportEmailDeliveryDto>> Send(Guid batchId, SendReportEmailsRequest request, CancellationToken token) => service.SendAsync(batchId, request, token);
    [HttpGet("batches/{batchId:guid}/deliveries"), Permission(PermissionModules.ReportDistribution, PermissionAction.View)] public Task<IReadOnlyList<ReportEmailDeliveryDto>> Deliveries(Guid batchId, CancellationToken token) => service.ListDeliveriesAsync(batchId, token);
    [HttpGet("deliveries"), Permission(PermissionModules.ReportDistribution, PermissionAction.View)] public Task<PagedResult<ReportEmailDeliveryDto>> History([FromQuery] ReportEmailHistoryRequest request, CancellationToken token) => service.SearchDeliveriesAsync(request, token);
    [HttpPost("deliveries/{id:guid}/resend"), Permission(PermissionModules.ReportDistribution, PermissionAction.Edit)] public Task<ReportEmailDeliveryDto> Resend(Guid id, CancellationToken token) => service.ResendAsync(id, token);
}
