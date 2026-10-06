using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;
using MobileBill.Domain.Enums;

namespace MobileBill.Api.Controllers;

[ApiController, Authorize]
public abstract class MasterDataControllerBase(IMasterDataService service) : ControllerBase
{
    protected const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    protected const long MaxImportBytes = 10L * 1024 * 1024;
    protected readonly IMasterDataService Service = service;

    // Excel import: commit=false only checks the file and lists problems; commit=true saves it if every row is valid.
    protected static async Task<MasterDataImportResult> ImportAsync(IFormFile? file, Func<Stream, Task<MasterDataImportResult>> import)
    {
        if (file is null || file.Length == 0) throw new MasterDataValidationException("Choose an Excel file (.xlsx) to upload.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase)) throw new MasterDataValidationException("Only Excel .xlsx files can be imported.");
        await using var stream = file.OpenReadStream();
        return await import(stream);
    }
}

[Route("api/employees")]
public sealed class EmployeesController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<EmployeeDto>> Get([FromQuery] PagedRequest request, CancellationToken token) => Service.GetEmployeesAsync(request, token);
    [HttpGet("{id:guid}")] public Task<EmployeeDto> Get(Guid id, CancellationToken token) => Service.GetEmployeeAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<ActionResult<EmployeeDto>> Create(EmployeeUpsertRequest request, CancellationToken token) { var item = await Service.CreateEmployeeAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.MasterEditors)] public Task<EmployeeDto> Update(Guid id, EmployeeUpsertRequest request, CancellationToken token) => Service.UpdateEmployeeAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateEmployeeAsync(id, token); return NoContent(); }
    [HttpGet("import/template"), Authorize(Roles = MasterDataRoles.MasterEditors)]
    public IActionResult ImportTemplate([FromServices] IMasterDataImportService importer) { var template = importer.EmployeeTemplate(); return File(template.Content, ExcelContentType, template.FileName); }
    [HttpPost("import"), Authorize(Roles = MasterDataRoles.MasterEditors), RequestSizeLimit(MaxImportBytes), RequestFormLimits(MultipartBodyLengthLimit = MaxImportBytes)]
    public Task<MasterDataImportResult> Import(IFormFile? file, [FromQuery] bool commit, [FromServices] IMasterDataImportService importer, CancellationToken token) =>
        ImportAsync(file, stream => importer.ImportEmployeesAsync(stream, commit, token));
    // Releases the employee's numbers to the SIM Pool and deactivates them; returns the pooled numbers.
    [HttpPost("{id:guid}/resign"), Authorize(Roles = MasterDataRoles.MasterEditors)] public Task<IReadOnlyList<MobileAccountDto>> Resign(Guid id, ResignEmployeeRequest request, CancellationToken token) => Service.ResignEmployeeAsync(id, request, token);
}

[Route("api/mobile-accounts")]
public sealed class MobileAccountsController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<MobileAccountDto>> Get([FromQuery] PagedRequest request, [FromQuery] SimStatus? status, CancellationToken token) => Service.GetMobileAccountsAsync(request, token, status);
    // Creating allocations and setting amounts is for Administrator and IT Engineer only, so is the import.
    [HttpGet("import/template"), Authorize(Roles = MasterDataRoles.Editors)]
    public IActionResult ImportTemplate([FromServices] IMasterDataImportService importer) { var template = importer.MobileAccountTemplate(); return File(template.Content, ExcelContentType, template.FileName); }
    [HttpPost("import"), Authorize(Roles = MasterDataRoles.Editors), RequestSizeLimit(MaxImportBytes), RequestFormLimits(MultipartBodyLengthLimit = MaxImportBytes)]
    public Task<MasterDataImportResult> Import(IFormFile? file, [FromQuery] bool commit, [FromServices] IMasterDataImportService importer, CancellationToken token) =>
        ImportAsync(file, stream => importer.ImportMobileAccountsAsync(stream, commit, token));
    // Numbers waiting in the SIM Pool, oldest first, with days in pool and the long-idle flag.
    [HttpGet("pool")] public Task<IReadOnlyList<SimPoolItemDto>> Pool(CancellationToken token) => Service.GetSimPoolAsync(token);
    [HttpPost("{id:guid}/release-to-pool"), Authorize(Roles = MasterDataRoles.MasterEditors)] public Task<MobileAccountDto> ReleaseToPool(Guid id, ReleaseToPoolRequest request, CancellationToken token) => Service.ReleaseToPoolAsync(id, request, token);
    [HttpPost("{id:guid}/assign-from-pool"), Authorize(Roles = MasterDataRoles.MasterEditors)]
    public async Task<ActionResult<MobileAccountDto>> AssignFromPool(Guid id, AssignFromPoolRequest request, CancellationToken token)
    {
        // HR and Finance users may give a pooled number to a new holder, but only on its existing credit limit and rental.
        var changesAmounts = request.MonthlyCreditLimit is not null || request.MonthlyRental is not null;
        if (changesAmounts && !User.IsInRole(nameof(UserRole.Administrator)) && !User.IsInRole(nameof(UserRole.ITEngineer))) return Forbid();
        return await Service.AssignFromPoolAsync(id, request, token);
    }
    [HttpPost("{id:guid}/disconnect"), Authorize(Roles = MasterDataRoles.MasterEditors)] public Task<MobileAccountDto> Disconnect(Guid id, DisconnectSimRequest request, CancellationToken token) => Service.DisconnectSimAsync(id, request, token);
    [HttpGet("{id:guid}")] public Task<MobileAccountDto> Get(Guid id, CancellationToken token) => Service.GetMobileAccountAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.Editors)] public async Task<ActionResult<MobileAccountDto>> Create(MobileAccountUpsertRequest request, CancellationToken token) { var item = await Service.CreateMobileAccountAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.Editors)] public Task<MobileAccountDto> Update(Guid id, MobileAccountUpsertRequest request, CancellationToken token) => Service.UpdateMobileAccountAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.Editors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateMobileAccountAsync(id, token); return NoContent(); }
    // Moves the number to another employee; the number, credit limit and rental stay as they are.
    [HttpPost("{id:guid}/reassign"), Authorize(Roles = MasterDataRoles.MasterEditors)] public Task<MobileAccountDto> Reassign(Guid id, MobileAccountReassignRequest request, CancellationToken token) => Service.ReassignMobileAccountAsync(id, request, token);
}

[Route("api/factories")]
public sealed class FactoriesController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<ReferenceDataDto>> Get([FromQuery] PagedRequest request, CancellationToken token) => Service.GetFactoriesAsync(request, token);
    [HttpGet("{id:guid}")] public Task<ReferenceDataDto> Get(Guid id, CancellationToken token) => Service.GetFactoryAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<ActionResult<ReferenceDataDto>> Create(ReferenceDataUpsertRequest request, CancellationToken token) { var item = await Service.CreateFactoryAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.MasterEditors)] public Task<ReferenceDataDto> Update(Guid id, ReferenceDataUpsertRequest request, CancellationToken token) => Service.UpdateFactoryAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateFactoryAsync(id, token); return NoContent(); }
}

[Route("api/departments")]
public sealed class DepartmentsController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<ReferenceDataDto>> Get([FromQuery] PagedRequest request, CancellationToken token) => Service.GetDepartmentsAsync(request, token);
    [HttpGet("{id:guid}")] public Task<ReferenceDataDto> Get(Guid id, CancellationToken token) => Service.GetDepartmentAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<ActionResult<ReferenceDataDto>> Create(ReferenceDataUpsertRequest request, CancellationToken token) { var item = await Service.CreateDepartmentAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.MasterEditors)] public Task<ReferenceDataDto> Update(Guid id, ReferenceDataUpsertRequest request, CancellationToken token) => Service.UpdateDepartmentAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateDepartmentAsync(id, token); return NoContent(); }
}

[Route("api/sections")]
public sealed class SectionsController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<SectionDto>> Get([FromQuery] PagedRequest request, [FromQuery] string? departmentCode, CancellationToken token) => Service.GetSectionsAsync(request, departmentCode, token);
    [HttpGet("{id:guid}")] public Task<SectionDto> Get(Guid id, CancellationToken token) => Service.GetSectionAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<ActionResult<SectionDto>> Create(SectionUpsertRequest request, CancellationToken token) { var item = await Service.CreateSectionAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.MasterEditors)] public Task<SectionDto> Update(Guid id, SectionUpsertRequest request, CancellationToken token) => Service.UpdateSectionAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateSectionAsync(id, token); return NoContent(); }
}

[Route("api/sub-sections")]
public sealed class SubSectionsController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<SubSectionDto>> Get([FromQuery] PagedRequest request, [FromQuery] string? sectionCode, CancellationToken token) => Service.GetSubSectionsAsync(request, sectionCode, token);
    [HttpGet("{id:guid}")] public Task<SubSectionDto> Get(Guid id, CancellationToken token) => Service.GetSubSectionAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<ActionResult<SubSectionDto>> Create(SubSectionUpsertRequest request, CancellationToken token) { var item = await Service.CreateSubSectionAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.MasterEditors)] public Task<SubSectionDto> Update(Guid id, SubSectionUpsertRequest request, CancellationToken token) => Service.UpdateSubSectionAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateSubSectionAsync(id, token); return NoContent(); }
}

[Route("api/designations")]
public sealed class DesignationsController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<ReferenceDataDto>> Get([FromQuery] PagedRequest request, CancellationToken token) => Service.GetDesignationsAsync(request, token);
    [HttpGet("{id:guid}")] public Task<ReferenceDataDto> Get(Guid id, CancellationToken token) => Service.GetDesignationAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<ActionResult<ReferenceDataDto>> Create(ReferenceDataUpsertRequest request, CancellationToken token) { var item = await Service.CreateDesignationAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.MasterEditors)] public Task<ReferenceDataDto> Update(Guid id, ReferenceDataUpsertRequest request, CancellationToken token) => Service.UpdateDesignationAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateDesignationAsync(id, token); return NoContent(); }
}

[Route("api/categories")]
public sealed class CategoriesController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<ReferenceDataDto>> Get([FromQuery] PagedRequest request, CancellationToken token) => Service.GetCategoriesAsync(request, token);
    [HttpGet("{id:guid}")] public Task<ReferenceDataDto> Get(Guid id, CancellationToken token) => Service.GetCategoryAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<ActionResult<ReferenceDataDto>> Create(ReferenceDataUpsertRequest request, CancellationToken token) { var item = await Service.CreateCategoryAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.MasterEditors)] public Task<ReferenceDataDto> Update(Guid id, ReferenceDataUpsertRequest request, CancellationToken token) => Service.UpdateCategoryAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.MasterEditors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateCategoryAsync(id, token); return NoContent(); }
}

[Route("api/providers")]
public sealed class ProvidersController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<ReferenceDataDto>> Get([FromQuery] PagedRequest request, CancellationToken token) => Service.GetProvidersAsync(request, token);
    [HttpGet("{id:guid}")] public Task<ReferenceDataDto> Get(Guid id, CancellationToken token) => Service.GetProviderAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.Editors)] public async Task<ActionResult<ReferenceDataDto>> Create(ReferenceDataUpsertRequest request, CancellationToken token) { var item = await Service.CreateProviderAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.Editors)] public Task<ReferenceDataDto> Update(Guid id, ReferenceDataUpsertRequest request, CancellationToken token) => Service.UpdateProviderAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.Editors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateProviderAsync(id, token); return NoContent(); }
}
