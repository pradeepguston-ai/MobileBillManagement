using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Reports;

namespace MobileBill.Api.Controllers;

[ApiController]
[Route("api/reports/billing")]
public sealed class ReportsController(IBillingExcelReportService reportService) : ControllerBase
{
    [HttpGet("{batchId:guid}/excel")]
    public async Task<IActionResult> ExportExcel(Guid batchId, CancellationToken cancellationToken)
    {
        var report = await reportService.ExportAsync(new BillingExcelReportRequest(batchId), cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }
}
