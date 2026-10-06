using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Reports;

namespace MobileBill.Api.Controllers;

// Value Added Services report. View-only, so every signed-in role may use it, like the other reports.
[ApiController, Authorize]
[Route("api/reports/vas")]
public sealed class VasReportsController(IVasReportService vasReports) : ControllerBase
{
    [HttpGet("batches")]
    public Task<IReadOnlyList<VasBatchDto>> Batches(CancellationToken token) => vasReports.GetBatchesAsync(token);

    [HttpGet("{batchId:guid}")]
    public Task<VasReportDto> Get(Guid batchId, [FromQuery] string[]? factoryCode, [FromQuery] string[]? categoryCode, [FromQuery] string[]? sectionCode, [FromQuery] decimal? minimumVas, [FromQuery] bool repeatOnly, CancellationToken token) =>
        vasReports.GetAsync(new VasReportRequest(batchId, factoryCode, categoryCode, sectionCode, minimumVas, repeatOnly), token);

    [HttpGet("{batchId:guid}/excel")]
    public async Task<IActionResult> Excel(Guid batchId, [FromQuery] string[]? factoryCode, [FromQuery] string[]? categoryCode, [FromQuery] string[]? sectionCode, [FromQuery] decimal? minimumVas, [FromQuery] bool repeatOnly, CancellationToken token)
    {
        var file = await vasReports.ExportExcelAsync(new VasReportRequest(batchId, factoryCode, categoryCode, sectionCode, minimumVas, repeatOnly), token);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("{batchId:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid batchId, [FromQuery] string[]? factoryCode, [FromQuery] string[]? categoryCode, [FromQuery] string[]? sectionCode, [FromQuery] decimal? minimumVas, [FromQuery] bool repeatOnly, CancellationToken token)
    {
        var file = await vasReports.ExportPdfAsync(new VasReportRequest(batchId, factoryCode, categoryCode, sectionCode, minimumVas, repeatOnly), token);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
