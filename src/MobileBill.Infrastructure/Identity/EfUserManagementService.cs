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

    public async Task<IReadOnlyList<ManagedUserDto>> GetUsersAsync(string? search, CancellationToken token)
    {
        var query = db.Users.AsNoTracking();
        var term = search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term))
            query = query.Where(x => x.Email.ToLower().Contains(term) || x.DisplayName.ToLower().Contains(term));
        return await query
            .OrderBy(x => x.DisplayName)
            .Select(x => new ManagedUserDto(x.Id, x.Email, x.DisplayName, x.Role, x.Status, x.CreatedAtUtc, x.UpdatedAtUtc))
            .ToListAsync(token);
    }

    public async Task<UserDto> ChangeRoleAsync(Guid userId, ChangeUserRoleRequest request, CancellationToken token)
    {
        var user = await Find(userId, token);
        if (IsCurrentUser(user) && request.Role != UserRole.Administrator)
            throw new UserManagementConflictException("You cannot remove your own Administrator role.");
        user.Role = request.Role;
        user.UpdatedAtUtc = clock.UtcNow;
        user.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(token);
        return Map(user);
    }

    private bool IsCurrentUser(User user) => string.Equals(user.Id.ToString(), currentUser.UserId, StringComparison.OrdinalIgnoreCase);

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
        if (IsCurrentUser(user))
            throw new UserManagementConflictException("You cannot deactivate your own account.");
        user.Status = UserAccountStatus.Deactivated;
        user.UpdatedAtUtc = clock.UtcNow;
        user.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(token);
        return Map(user);
    }

    public async Task<IReadOnlyList<PasswordResetRequestDto>> GetPendingPasswordResetsAsync(CancellationToken token) =>
        await db.PasswordResetRequests.AsNoTracking()
            .Where(x => x.Status == PasswordResetStatus.Pending)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new PasswordResetRequestDto(x.Id, x.UserId, x.User.Email, x.User.DisplayName, x.User.Role, x.CreatedAtUtc))
            .ToListAsync(token);

    public async Task ApprovePasswordResetAsync(Guid requestId, CancellationToken token)
    {
        var request = await FindPendingReset(requestId, token);
        if (request.User.Status != UserAccountStatus.Active)
            throw new PasswordResetConflictException("Only active accounts can have their password reset.");
        var now = clock.UtcNow;
        request.User.PasswordHash = request.NewPasswordHash;
        request.User.UpdatedAtUtc = now;
        request.User.UpdatedBy = currentUser.UserId;
        Decide(request, PasswordResetStatus.Approved, now);
        await db.SaveChangesAsync(token);
    }

    public async Task RejectPasswordResetAsync(Guid requestId, CancellationToken token)
    {
        var request = await FindPendingReset(requestId, token);
        Decide(request, PasswordResetStatus.Rejected, clock.UtcNow);
        await db.SaveChangesAsync(token);
    }

    private async Task<PasswordResetRequest> FindPendingReset(Guid id, CancellationToken token)
    {
        var request = await db.PasswordResetRequests.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id, token)
            ?? throw new PasswordResetRequestNotFoundException(id);
        if (request.Status != PasswordResetStatus.Pending)
            throw new PasswordResetConflictException("This password reset request has already been decided or replaced.");
        return request;
    }

    private void Decide(PasswordResetRequest request, PasswordResetStatus status, DateTimeOffset now)
    {
        request.Status = status;
        request.DecidedAtUtc = now;
        request.DecidedBy = currentUser.UserId;
        request.UpdatedAtUtc = now;
        request.UpdatedBy = currentUser.UserId;
    }

    private async Task<User> Find(Guid id, CancellationToken token) =>
        await db.Users.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new UserNotFoundException(id);

    private static UserDto Map(User user) => new(user.Id, user.Email, user.DisplayName, user.Role, user.Status);
}
