using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Identity;

public interface IUserSessionValidator
{
    Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}

// A signed token stays valid until it expires, so without this check a deactivated user, or one whose role was
// lowered, could keep using an old token for the rest of its lifetime.
public sealed class EfUserSessionValidator(MobileBillDbContext db, IOptions<JwtOptions> options) : IUserSessionValidator
{
    public async Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!options.Value.ValidateUserOnEachRequest) return true;
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return false;
        var tokenRole = principal.FindFirstValue(ClaimTypes.Role);

        var user = await db.Users.AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new { x.Status, x.Role })
            .SingleOrDefaultAsync(cancellationToken);
        return user is not null
            && user.Status == UserAccountStatus.Active
            && string.Equals(user.Role.ToString(), tokenRole, StringComparison.Ordinal);
    }
}
