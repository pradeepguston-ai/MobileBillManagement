using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.Identity;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Identity;

public sealed class EfUserManagementService(MobileBillDbContext db, ICurrentUserService currentUser, IClock clock) : IUserManagementService
{
    public async Task<IReadOnlyList<PendingUserDto>> GetPendingUsersAsync(CancellationToken token) =>
        await db.Users.AsNoTracking()
            .Where(x => x.Status == UserAccountStatus.PendingActivation)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new PendingUserDto(x.Id, x.Email, x.DisplayName, x.Role, x.CreatedAtUtc))
            .ToListAsync(token);

    public async Task<UserDto> ActivateUserAsync(Guid userId, ActivateUserRequest request, CancellationToken token)
    {
        var user = await Find(userId, token);
        if (request.RoleOverride is not null) user.Role = request.RoleOverride.Value;
        user.Status = UserAccountStatus.Active;
        user.UpdatedAtUtc = clock.UtcNow;
        user.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(token);
        return Map(user);
    }

    public async Task<UserDto> DeactivateUserAsync(Guid userId, CancellationToken token)
    {
        var user = await Find(userId, token);
        user.Status = UserAccountStatus.Deactivated;
        user.UpdatedAtUtc = clock.UtcNow;
        user.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(token);
        return Map(user);
    }

    private async Task<User> Find(Guid id, CancellationToken token) =>
        await db.Users.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new UserNotFoundException(id);

    private static UserDto Map(User user) => new(user.Id, user.Email, user.DisplayName, user.Role, user.Status);
}
