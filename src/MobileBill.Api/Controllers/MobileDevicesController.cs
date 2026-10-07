using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Common;
using MobileBill.Application.Devices;
using MobileBill.Domain.Enums;

namespace MobileBill.Api.Controllers;

// Company mobile devices. Administrator and IT Engineer register, issue, return and replace them; every other role
// views the register, the history and the devices to collect from leavers, and downloads the register report.
[ApiController, Authorize]
[Route("api/mobile-devices")]
public sealed class MobileDevicesController(IDeviceService service) : ControllerBase
{
    [HttpGet] public Task<PagedResult<MobileDeviceDto>> Get([FromQuery] PagedRequest request, [FromQuery] DeviceStatus? status, CancellationToken token) => service.GetDevicesAsync(request, token, status);
    // Devices that can be issued or given as a replacement.
    [HttpGet("in-stock")] public Task<PagedResult<MobileDeviceDto>> InStock([FromQuery] PagedRequest request, CancellationToken token) => service.GetDevicesAsync(request with { IsActive = null }, token, DeviceStatus.InStock);
    [HttpGet("issues")] public Task<PagedResult<DeviceIssueDto>> Issues([FromQuery] PagedRequest request, CancellationToken token) => service.GetIssuesAsync(request, token);
    [HttpGet("to-collect")] public Task<IReadOnlyList<DeviceToCollectDto>> ToCollect(CancellationToken token) => service.GetToCollectAsync(token);
    [HttpGet("register/excel")] public async Task<IActionResult> RegisterExcel([FromQuery] DeviceRegisterRequest request, CancellationToken token) => File(await service.ExportRegisterExcelAsync(request, token));
    [HttpGet("register/pdf")] public async Task<IActionResult> RegisterPdf([FromQuery] DeviceRegisterRequest request, CancellationToken token) => File(await service.ExportRegisterPdfAsync(request, token));
    [HttpGet("{id:guid}")] public Task<MobileDeviceDto> Get(Guid id, CancellationToken token) => service.GetDeviceAsync(id, token);

    [HttpPost, Authorize(Roles = MasterDataRoles.Editors)] public async Task<ActionResult<MobileDeviceDto>> Create(MobileDeviceUpsertRequest request, CancellationToken token) { var item = await service.CreateDeviceAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.Editors)] public Task<MobileDeviceDto> Update(Guid id, MobileDeviceUpsertRequest request, CancellationToken token) => service.UpdateDeviceAsync(id, request, token);
    [HttpPost("{id:guid}/issue"), Authorize(Roles = MasterDataRoles.Editors)] public Task<MobileDeviceDto> Issue(Guid id, IssueDeviceRequest request, CancellationToken token) => service.IssueAsync(id, request, token);
    [HttpPost("{id:guid}/return"), Authorize(Roles = MasterDataRoles.Editors)] public Task<MobileDeviceDto> Return(Guid id, ReturnDeviceRequest request, CancellationToken token) => service.ReturnAsync(id, request, token);
    // Returns the replacement device, now issued to the same employee.
    [HttpPost("{id:guid}/replace"), Authorize(Roles = MasterDataRoles.Editors)] public Task<MobileDeviceDto> Replace(Guid id, ReplaceDeviceRequest request, CancellationToken token) => service.ReplaceAsync(id, request, token);
    [HttpPost("{id:guid}/lost"), Authorize(Roles = MasterDataRoles.Editors)] public Task<MobileDeviceDto> Lost(Guid id, MarkDeviceLostRequest request, CancellationToken token) => service.MarkLostAsync(id, request, token);
    [HttpPost("{id:guid}/repaired"), Authorize(Roles = MasterDataRoles.Editors)] public Task<MobileDeviceDto> Repaired(Guid id, DeviceStatusChangeRequest request, CancellationToken token) => service.MarkRepairedAsync(id, request, token);
    [HttpPost("{id:guid}/retire"), Authorize(Roles = MasterDataRoles.Editors)] public Task<MobileDeviceDto> Retire(Guid id, DeviceStatusChangeRequest request, CancellationToken token) => service.RetireAsync(id, request, token);

    private FileContentResult File(DeviceReportFile file) => File(file.Content, file.ContentType, file.FileName);
}
