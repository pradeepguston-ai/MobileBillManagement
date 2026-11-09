using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Activity;
using MobileBill.Application.Common;

namespace MobileBill.Api.Controllers;

// The Activity Log: what every user did, newest first. Every signed-in user can read it.
[ApiController, Route("api/activity-log"), Authorize]
public sealed class ActivityLogController(IActivityLogService activityLog) : ControllerBase
{
    [HttpGet] public Task<PagedResult<ActivityLogEntryDto>> Search([FromQuery] ActivityLogRequest request, CancellationToken token) => activityLog.SearchAsync(request, token);
    [HttpGet("filters")] public Task<ActivityLogFilterOptionsDto> Filters(CancellationToken token) => activityLog.GetFilterOptionsAsync(token);
}
