using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobileBill.Application.Identity;

namespace MobileBill.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register"), AllowAnonymous]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request, CancellationToken token)
    {
        var user = await authService.RegisterAsync(request, token);
        return CreatedAtAction(nameof(Register), user);
    }

    [HttpPost("login"), AllowAnonymous]
    public Task<AuthResultDto> Login(LoginRequest request, CancellationToken token) => authService.LoginAsync(request, token);

    [HttpGet("me"), Authorize]
    public Task<UserDto> Me(CancellationToken token)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return authService.GetCurrentUserAsync(userId, token);
    }
}
