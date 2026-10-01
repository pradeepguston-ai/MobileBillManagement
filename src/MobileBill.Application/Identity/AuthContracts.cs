using System.Text.Json.Serialization;
using MobileBill.Domain.Enums;

namespace MobileBill.Application.Identity;

public sealed record RegisterRequest(string Email, string Password, string DisplayName, [property: JsonConverter(typeof(JsonStringEnumConverter))] UserRole RequestedRole);
public sealed record LoginRequest(string Email, string Password);
public sealed record UserDto(Guid Id, string Email, string DisplayName, [property: JsonConverter(typeof(JsonStringEnumConverter))] UserRole Role, [property: JsonConverter(typeof(JsonStringEnumConverter))] UserAccountStatus Status);
public sealed record AuthResultDto(string Token, DateTimeOffset ExpiresAtUtc, UserDto User);
public sealed record PendingUserDto(Guid Id, string Email, string DisplayName, [property: JsonConverter(typeof(JsonStringEnumConverter))] UserRole RequestedRole, DateTimeOffset RegisteredAtUtc);
public sealed record ActivateUserRequest([property: JsonConverter(typeof(JsonStringEnumConverter))] UserRole? RoleOverride);

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IUserManagementService
{
    Task<IReadOnlyList<PendingUserDto>> GetPendingUsersAsync(CancellationToken cancellationToken);
    Task<UserDto> ActivateUserAsync(Guid userId, ActivateUserRequest request, CancellationToken cancellationToken);
    Task<UserDto> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken);
}
