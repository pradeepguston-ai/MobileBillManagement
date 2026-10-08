using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MobileBill.Api.Configuration;
using MobileBill.Application.Assistant;

namespace MobileBill.Api.Controllers;

// The AI assistant, for every signed-in user. It only reads data; see AssistantService.
[ApiController, Route("api/assistant"), Authorize]
public sealed class AssistantController(IAssistantService assistant) : ControllerBase
{
    [HttpGet("status")]
    public AssistantStatusDto Status() => assistant.GetStatus();

    [HttpPost("ask"), EnableRateLimiting(RateLimitPolicies.Assistant)]
    public Task<AssistantReplyDto> Ask(AssistantAskRequest request, CancellationToken token) => assistant.AskAsync(request, token);
}
