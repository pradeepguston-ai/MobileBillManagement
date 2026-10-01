using System.Text.Json.Serialization;
using MobileBill.Domain.Enums;

namespace MobileBill.Application.Identity;

public sealed record RegisterRequest(string Email, string Password, string DisplayName, [property: JsonConverter(typeof(JsonStringEnumConverter))] UserRole RequestedRole);
public sealed record LoginRequest(string Email, string Password);
public sealed record UserDto(Guid Id, string Email, string DisplayName, [property: JsonConverter(typeof(JsonStringEnumConverter))] UserRole Role, [property: JsonConverter(typeof(JsonStringEnumConverter))] UserAccountStatus Status);
public sealed record AuthResultDto(string Token, DateTimeOffset ExpiresAtUtc, UserDto User);
public sealed record PendingUserDto(Guid Id, string Email, string DisplayName, [property: JsonConverter(typeof(JsonStringEnumConverter))] UserRole RequestedRole, DateTimeOffset RegisteredAtUtc);
public sealed record ActivateUserRequest([property: JsonConverter(typeof(JsonStringEnumConverter))] UserRole? RoleOverride);
public sealed record ManagedUserDto(Guid Id, string Email, string DisplayName, [property: JsonConverter(typeof(JsonStringEnumConverter))] UserRole Role, [property: JsonConverter(typeof(JsonStringEnumConverter))] UserAccountStatus Status, DateTimeOffset RegisteredAtUtc, DateTimeOffset? UpdatedAtUtc);
public sealed record ChangeUserRoleRequest([property: JsonConverter(typeof(JsonStringEnumConverter))] UserRole Role);
public sealed record ForgotPasswordRequest(string Email, string NewPassword);
public sealed record PasswordResetRequestDto(Guid Id, Guid UserId, string Email, string DisplayName, [property: JsonConverter(typeof(JsonStringEnumConverter))] UserRole Role, DateTimeOffset RequestedAtUtc);

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
    Task RequestPasswordResetAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);
}

public interface IUserManagementService
{
    Task<IReadOnlyList<PendingUserDto>> GetPendingUsersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ManagedUserDto>> GetUsersAsync(string? search, CancellationToken cancellationToken);
    Task<UserDto> ChangeRoleAsync(Guid userId, ChangeUserRoleRequest request, CancellationToken cancellationToken);
    Task<UserDto> ActivateUserAsync(Guid userId, ActivateUserRequest request, CancellationToken cancellationToken);
    Task<UserDto> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PasswordResetRequestDto>> GetPendingPasswordResetsAsync(CancellationToken cancellationToken);
    Task ApprovePasswordResetAsync(Guid requestId, CancellationToken cancellationToken);
    Task RejectPasswordResetAsync(Guid requestId, CancellationToken cancellationToken);
}
