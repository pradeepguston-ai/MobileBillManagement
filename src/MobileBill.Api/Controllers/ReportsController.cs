using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Reports;

namespace MobileBill.Api.Controllers;

[ApiController, Authorize]
[Route("api/reports/billing")]
public sealed class ReportsController(IBillingExcelReportService excelReportService, IBillingPdfReportService pdfReportService) : ControllerBase
{
    [HttpGet("{batchId:guid}/excel")]
    public async Task<IActionResult> ExportExcel(Guid batchId, [FromQuery] string[]? factoryCode, [FromQuery] string[]? categoryCode, [FromQuery] string[]? sectionCode, [FromQuery] bool includeVas, CancellationToken cancellationToken)
    {
        var report = await excelReportService.ExportAsync(new BillingExcelReportRequest(batchId, factoryCode, categoryCode, sectionCode, includeVas), cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [HttpGet("{batchId:guid}/pdf")]
    public async Task<IActionResult> ExportPdf(Guid batchId, [FromQuery] string[]? factoryCode, [FromQuery] string[]? categoryCode, [FromQuery] string[]? sectionCode, CancellationToken cancellationToken)
    {
        var report = await pdfReportService.ExportAsync(new BillingExcelReportRequest(batchId, factoryCode, categoryCode, sectionCode), cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }
}
