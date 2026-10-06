using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Billing;
using MobileBill.Application.Common;
using MobileBill.Application.Reports;
namespace MobileBill.Api.Controllers;
[ApiController,Route("api/bill-batches"),Authorize]
public sealed class BillBatchesController(IBillBatchService service, IBillLinesExcelExportService linesExport):ControllerBase
{
 // Bill PDFs are capped at 20 MB by BillStorage; this stops larger request bodies before they are read.
 private const long MaxUploadRequestBytes=25L*1024*1024;
 [HttpGet] public Task<PagedResult<BillBatchListItemDto>> List([FromQuery] BillBatchListRequest request,CancellationToken t)=>service.ListAsync(request,t);
 [HttpPost,Authorize] public async Task<ActionResult<BillBatchDto>> Create(CreateBillBatchRequest request,CancellationToken t){var b=await service.CreateAsync(request,t);return CreatedAtAction(nameof(Get),new{id=b.Id},b);}
 [HttpPost("{id:guid}/upload"),Authorize,RequestSizeLimit(MaxUploadRequestBytes),RequestFormLimits(MultipartBodyLengthLimit=MaxUploadRequestBytes)] public async Task<BillBatchDto> Upload(Guid id,[FromForm] IFormFile file,CancellationToken t){await using var s=file?.OpenReadStream()??throw new BillBatchValidationException("A PDF file is required.");return await service.UploadAsync(id,s,file.FileName,file.ContentType,file.Length,t);}
 [HttpPost("{id:guid}/parse"),Authorize] public Task<BillBatchDto> Parse(Guid id,CancellationToken t)=>service.ParseAsync(id,t);
 [HttpGet("{id:guid}")] public Task<BillBatchDto> Get(Guid id,CancellationToken t)=>service.GetAsync(id,t);
 [HttpGet("{id:guid}/lines")] public Task<PagedResult<BillLineDto>> Lines(Guid id,[FromQuery]PagedRequest r,CancellationToken t)=>service.GetLinesAsync(id,r,t);
 [HttpGet("{id:guid}/lines/excel")] public async Task<IActionResult> LinesExcel(Guid id,[FromQuery] string? search,CancellationToken t){var file=await linesExport.ExportAsync(id,search,t);return File(file.Content,file.ContentType,file.FileName);}
 [HttpPost("{id:guid}/validate"),Authorize] public Task<BillBatchDto> Validate(Guid id,CancellationToken t)=>service.ValidateAsync(id,t);
}
