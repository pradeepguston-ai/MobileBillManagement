using Microsoft.EntityFrameworkCore;
using MobileBill.Application.Common;
using MobileBill.Application.Identity;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.Infrastructure.Identity;

public sealed class EfAuthService(MobileBillDbContext db, IPasswordHasherService hasher, ITokenService tokenService, IClock clock) : IAuthService
{
    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken token)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new UserManagementConflictException("A valid email, display name, and a password of at least 8 characters are required.");
        if (request.RequestedRole == UserRole.Administrator)
            throw new UserManagementConflictException("The Administrator role cannot be self-registered.");
        if (await db.Users.AnyAsync(x => x.Email == email, token))
            throw new EmailAlreadyRegisteredException(email);

        var now = clock.UtcNow;
        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            Role = request.RequestedRole,
            Status = UserAccountStatus.PendingActivation,
            PasswordHash = string.Empty,
            CreatedAtUtc = now
        };
        user.PasswordHash = hasher.Hash(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(token);
        return Map(user);
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken token)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, token)
            ?? throw new InvalidCredentialsException();
        if (!hasher.Verify(user, request.Password))
            throw new InvalidCredentialsException();
        if (user.Status != UserAccountStatus.Active)
            throw new AccountNotActiveException(user.Status);

        var (accessToken, expiresAtUtc) = tokenService.CreateAccessToken(user);
        return new AuthResultDto(accessToken, expiresAtUtc, Map(user));
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken token)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId, token) ?? throw new UserNotFoundException(userId);
        return Map(user);
    }


    // Always completes silently for unknown or inactive accounts so the endpoint cannot be used to discover which emails are registered.
    public async Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken token)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            throw new UserManagementConflictException("A valid email and a new password of at least 8 characters are required.");

        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email && x.Status == UserAccountStatus.Active, token);
        if (user is null) return;

        var now = clock.UtcNow;
        var earlier = await db.PasswordResetRequests.Where(x => x.UserId == user.Id && x.Status == PasswordResetStatus.Pending).ToListAsync(token);
        foreach (var previous in earlier)
        {
            previous.Status = PasswordResetStatus.Superseded;
            previous.UpdatedAtUtc = now;
        }
        db.PasswordResetRequests.Add(new PasswordResetRequest
        {
            UserId = user.Id,
            NewPasswordHash = hasher.Hash(user, request.NewPassword),
            Status = PasswordResetStatus.Pending,
            CreatedAtUtc = now,
            CreatedBy = user.Id.ToString()
        });
        await db.SaveChangesAsync(token);
    }

    private static UserDto Map(User user) => new(user.Id, user.Email, user.DisplayName, user.Role, user.Status);
}
