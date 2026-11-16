using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.Identity;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Identity;

public sealed class EfUserManagementService(MobileBillDbContext db, ICurrentUserService currentUser, IClock clock, IPasswordHasherService hasher) : IUserManagementService
{
    // Same limits as self-registration, but the account is active at once and any role may be given (Administrator only by an Administrator).
    public async Task<ManagedUserDto> CreateUserAsync(CreateUserRequest request, CancellationToken token)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        var displayName = request.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > EfAuthService.MaxEmailLength || !email.Contains('@')
            || string.IsNullOrWhiteSpace(displayName) || displayName.Length > EfAuthService.MaxDisplayNameLength
            || !EfAuthService.IsAcceptablePassword(request.Password))
            throw new UserManagementConflictException($"A valid email, display name, and a password of {EfAuthService.MinPasswordLength} to {EfAuthService.MaxPasswordLength} characters are required.");
        if (!Enum.IsDefined(request.Role))
            throw new UserManagementConflictException("A valid role is required.");
        if (request.Role == UserRole.Administrator) RequireAdministrator("create Administrator accounts");
        if (await db.Users.AnyAsync(x => x.Email == email, token))
            throw new EmailAlreadyRegisteredException(email);

        var user = new User
        {
            Email = email,
            DisplayName = displayName,
            Role = request.Role,
            Status = UserAccountStatus.Active,
            PasswordHash = string.Empty,
            CreatedAtUtc = clock.UtcNow,
            CreatedBy = currentUser.UserId,
            MustChangePassword = true,
        };
        user.PasswordHash = hasher.Hash(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(token);
        return new ManagedUserDto(user.Id, user.Email, user.DisplayName, user.Role, user.Status, user.CreatedAtUtc, user.UpdatedAtUtc, user.MustChangePassword);
    }

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
            .Select(x => new ManagedUserDto(x.Id, x.Email, x.DisplayName, x.Role, x.Status, x.CreatedAtUtc, x.UpdatedAtUtc, x.MustChangePassword))
            .ToListAsync(token);
    }

    public async Task<UserDto> ChangeRoleAsync(Guid userId, ChangeUserRoleRequest request, CancellationToken token)
    {
        if (!Enum.IsDefined(request.Role)) throw new UserManagementConflictException("A valid role is required.");
        var user = await Find(userId, token);
        if (IsCurrentUser(user))
        {
            if (!IsAdministrator) throw new UserManagementForbiddenException("You cannot change your own role.");
            if (request.Role != UserRole.Administrator) throw new UserManagementConflictException("You cannot remove your own Administrator role.");
        }
        if (user.Role == UserRole.Administrator || request.Role == UserRole.Administrator) RequireAdministrator("change Administrator accounts or give the Administrator role");
        user.Role = request.Role;
        user.UpdatedAtUtc = clock.UtcNow;
        user.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(token);
        return Map(user);
    }

    private bool IsCurrentUser(User user) => string.Equals(user.Id.ToString(), currentUser.UserId, StringComparison.OrdinalIgnoreCase);

    // Other roles can be given the User Management permission; Administrator accounts and the Administrator role stay with
    // Administrators, so that permission can never be used to become or take over an Administrator.
    private bool IsAdministrator => currentUser.Role == UserRole.Administrator;

    private void RequireAdministrator(string what)
    {
        if (!IsAdministrator) throw new UserManagementForbiddenException($"Only an Administrator can {what}.");
    }

    public async Task<UserDto> ActivateUserAsync(Guid userId, ActivateUserRequest request, CancellationToken token)
    {
        if (request.RoleOverride is { } roleOverride && !Enum.IsDefined(roleOverride)) throw new UserManagementConflictException("A valid role is required.");
        var user = await Find(userId, token);
        if (user.Role == UserRole.Administrator || request.RoleOverride == UserRole.Administrator) RequireAdministrator("activate Administrator accounts or give the Administrator role");
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
        if (user.Role == UserRole.Administrator) RequireAdministrator("deactivate Administrator accounts");
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
        GuardReset(request);
        if (request.User.Status != UserAccountStatus.Active)
            throw new PasswordResetConflictException("Only active accounts can have their password reset.");
        var now = clock.UtcNow;
        request.User.PasswordHash = request.NewPasswordHash;
        // The user chose this password, so a pending one-time password no longer needs replacing.
        request.User.MustChangePassword = false;
        request.User.UpdatedAtUtc = now;
        request.User.UpdatedBy = currentUser.UserId;
        Decide(request, PasswordResetStatus.Approved, now);
        await db.SaveChangesAsync(token);
    }

    public async Task RejectPasswordResetAsync(Guid requestId, CancellationToken token)
    {
        var request = await FindPendingReset(requestId, token);
        GuardReset(request);
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

    // A password reset applies a password the requester chose, so a non-Administrator cannot decide one for an
    // Administrator account or for their own account.
    private void GuardReset(PasswordResetRequest request)
    {
        if (request.User.Role == UserRole.Administrator) RequireAdministrator("decide password resets for Administrator accounts");
        if (!IsAdministrator && IsCurrentUser(request.User)) throw new UserManagementForbiddenException("You cannot decide your own password reset.");
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
