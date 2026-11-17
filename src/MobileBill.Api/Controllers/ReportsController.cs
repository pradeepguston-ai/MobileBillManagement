using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Domain.Enums;
using MobileBill.Application.Identity;
using MobileBill.Api.Identity;
using MobileBill.Application.Billing;
using MobileBill.Application.Reports;

namespace MobileBill.Api.Controllers;

[ApiController, Authorize]
[Route("api/reports/billing")]
public sealed class ReportsController(IBillingExcelReportService excelReportService, IBillingPdfReportService pdfReportService, IBillingDeductionReportService deductionReportService) : ControllerBase
{
    [HttpGet("{batchId:guid}/excel"), Permission(PermissionModules.MonthlyBillReport, PermissionAction.View)]
    public async Task<IActionResult> ExportExcel(Guid batchId, [FromQuery] string[]? factoryCode, [FromQuery] string[]? categoryCode, [FromQuery] string[]? sectionCode, [FromQuery] bool includeVas, CancellationToken cancellationToken)
    {
        var report = await excelReportService.ExportAsync(new BillingExcelReportRequest(batchId, factoryCode, categoryCode, sectionCode, includeVas), cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [HttpGet("{batchId:guid}/pdf"), Permission(PermissionModules.MonthlyBillReport, PermissionAction.View)]
    public async Task<IActionResult> ExportPdf(Guid batchId, [FromQuery] string[]? factoryCode, [FromQuery] string[]? categoryCode, [FromQuery] string[]? sectionCode, CancellationToken cancellationToken)
    {
        var report = await pdfReportService.ExportAsync(new BillingExcelReportRequest(batchId, factoryCode, categoryCode, sectionCode), cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    // The payroll deduction sheet for the Open Report screen, narrowed by the same filters as its table.
    [HttpGet("{batchId:guid}/deduction-excel"), Permission(PermissionModules.MonthlyBillReport, PermissionAction.View)]
    public async Task<IActionResult> ExportDeductionExcel(Guid batchId, [FromQuery] BillReviewRowsRequest filters, CancellationToken cancellationToken)
    {
        var report = await deductionReportService.ExportAsync(batchId, filters, cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }
}
