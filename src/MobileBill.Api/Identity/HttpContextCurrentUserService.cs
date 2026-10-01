using System.Security.Claims;
using MobileBill.Application.Common;
using MobileBill.Domain.Enums;

namespace MobileBill.Api.Identity;

public sealed class HttpContextCurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal Principal => accessor.HttpContext?.User
        ?? throw new InvalidOperationException("No HTTP request is active for the current user.");

    public string UserId => Principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("The current request is not authenticated.");

    public string DisplayName => Principal.FindFirstValue(ClaimTypes.Name) ?? UserId;

    public UserRole Role => Enum.Parse<UserRole>(Principal.FindFirstValue(ClaimTypes.Role)
        ?? throw new InvalidOperationException("The current request has no role claim."));
}
