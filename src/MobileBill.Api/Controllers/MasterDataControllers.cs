using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Common;
using MobileBill.Application.MasterData;

namespace MobileBill.Api.Controllers;

[ApiController]
public abstract class MasterDataControllerBase(IMasterDataService service) : ControllerBase
{
    protected readonly IMasterDataService Service = service;
}

[Route("api/employees")]
public sealed class EmployeesController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<EmployeeDto>> Get([FromQuery] PagedRequest request, CancellationToken token) => Service.GetEmployeesAsync(request, token);
    [HttpGet("{id:guid}")] public Task<EmployeeDto> Get(Guid id, CancellationToken token) => Service.GetEmployeeAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.Editors)] public async Task<ActionResult<EmployeeDto>> Create(EmployeeUpsertRequest request, CancellationToken token) { var item = await Service.CreateEmployeeAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.Editors)] public Task<EmployeeDto> Update(Guid id, EmployeeUpsertRequest request, CancellationToken token) => Service.UpdateEmployeeAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.Editors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateEmployeeAsync(id, token); return NoContent(); }
}

[Route("api/mobile-accounts")]
public sealed class MobileAccountsController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<MobileAccountDto>> Get([FromQuery] PagedRequest request, CancellationToken token) => Service.GetMobileAccountsAsync(request, token);
    [HttpGet("{id:guid}")] public Task<MobileAccountDto> Get(Guid id, CancellationToken token) => Service.GetMobileAccountAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.Editors)] public async Task<ActionResult<MobileAccountDto>> Create(MobileAccountUpsertRequest request, CancellationToken token) { var item = await Service.CreateMobileAccountAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.Editors)] public Task<MobileAccountDto> Update(Guid id, MobileAccountUpsertRequest request, CancellationToken token) => Service.UpdateMobileAccountAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.Editors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateMobileAccountAsync(id, token); return NoContent(); }
}

[Route("api/factories")]
public sealed class FactoriesController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<ReferenceDataDto>> Get([FromQuery] PagedRequest request, CancellationToken token) => Service.GetFactoriesAsync(request, token);
    [HttpGet("{id:guid}")] public Task<ReferenceDataDto> Get(Guid id, CancellationToken token) => Service.GetFactoryAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.Editors)] public async Task<ActionResult<ReferenceDataDto>> Create(ReferenceDataUpsertRequest request, CancellationToken token) { var item = await Service.CreateFactoryAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.Editors)] public Task<ReferenceDataDto> Update(Guid id, ReferenceDataUpsertRequest request, CancellationToken token) => Service.UpdateFactoryAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.Editors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateFactoryAsync(id, token); return NoContent(); }
}

[Route("api/departments")]
public sealed class DepartmentsController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<ReferenceDataDto>> Get([FromQuery] PagedRequest request, CancellationToken token) => Service.GetDepartmentsAsync(request, token);
    [HttpGet("{id:guid}")] public Task<ReferenceDataDto> Get(Guid id, CancellationToken token) => Service.GetDepartmentAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.Editors)] public async Task<ActionResult<ReferenceDataDto>> Create(ReferenceDataUpsertRequest request, CancellationToken token) { var item = await Service.CreateDepartmentAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.Editors)] public Task<ReferenceDataDto> Update(Guid id, ReferenceDataUpsertRequest request, CancellationToken token) => Service.UpdateDepartmentAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.Editors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateDepartmentAsync(id, token); return NoContent(); }
}

[Route("api/designations")]
public sealed class DesignationsController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<ReferenceDataDto>> Get([FromQuery] PagedRequest request, CancellationToken token) => Service.GetDesignationsAsync(request, token);
    [HttpGet("{id:guid}")] public Task<ReferenceDataDto> Get(Guid id, CancellationToken token) => Service.GetDesignationAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.Editors)] public async Task<ActionResult<ReferenceDataDto>> Create(ReferenceDataUpsertRequest request, CancellationToken token) { var item = await Service.CreateDesignationAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.Editors)] public Task<ReferenceDataDto> Update(Guid id, ReferenceDataUpsertRequest request, CancellationToken token) => Service.UpdateDesignationAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.Editors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateDesignationAsync(id, token); return NoContent(); }
}

[Route("api/categories")]
public sealed class CategoriesController(IMasterDataService service) : MasterDataControllerBase(service)
{
    [HttpGet] public Task<PagedResult<ReferenceDataDto>> Get([FromQuery] PagedRequest request, CancellationToken token) => Service.GetCategoriesAsync(request, token);
    [HttpGet("{id:guid}")] public Task<ReferenceDataDto> Get(Guid id, CancellationToken token) => Service.GetCategoryAsync(id, token);
    [HttpPost, Authorize(Roles = MasterDataRoles.Editors)] public async Task<ActionResult<ReferenceDataDto>> Create(ReferenceDataUpsertRequest request, CancellationToken token) { var item = await Service.CreateCategoryAsync(request, token); return CreatedAtAction(nameof(Get), new { item.Id }, item); }
    [HttpPut("{id:guid}"), Authorize(Roles = MasterDataRoles.Editors)] public Task<ReferenceDataDto> Update(Guid id, ReferenceDataUpsertRequest request, CancellationToken token) => Service.UpdateCategoryAsync(id, request, token);
    [HttpPost("{id:guid}/deactivate"), Authorize(Roles = MasterDataRoles.Editors)] public async Task<IActionResult> Deactivate(Guid id, CancellationToken token) { await Service.DeactivateCategoryAsync(id, token); return NoContent(); }
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
