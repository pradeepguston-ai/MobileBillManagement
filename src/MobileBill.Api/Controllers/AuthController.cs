using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MobileBill.Api.Configuration;
using MobileBill.Application.Identity;

namespace MobileBill.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Authentication)]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request, CancellationToken token)
    {
        var user = await authService.RegisterAsync(request, token);
        return CreatedAtAction(nameof(Register), user);
    }

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Authentication)]
    public Task<AuthResultDto> Login(LoginRequest request, CancellationToken token) => authService.LoginAsync(request, token);

    [HttpPost("forgot-password"), AllowAnonymous, EnableRateLimiting(RateLimitPolicies.Authentication)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken token)
    {
        await authService.RequestPasswordResetAsync(request, token);
        return Accepted();
    }

    [HttpGet("me"), Authorize]
    public Task<UserDto> Me(CancellationToken token)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return authService.GetCurrentUserAsync(userId, token);
    }
}
