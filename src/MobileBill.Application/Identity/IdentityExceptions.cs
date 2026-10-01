namespace MobileBill.Application.Identity;

public sealed class EmailAlreadyRegisteredException(string email) : Exception($"An account with email '{email}' is already registered.");
public sealed class InvalidCredentialsException() : Exception("The email or password is incorrect.");
public sealed class AccountNotActiveException(MobileBill.Domain.Enums.UserAccountStatus status) : Exception(status == MobileBill.Domain.Enums.UserAccountStatus.PendingActivation
    ? "This account is pending administrator activation."
    : "This account has been deactivated.");
public sealed class UserNotFoundException(Guid userId) : Exception($"User '{userId}' was not found.");
public sealed class UserManagementConflictException(string message) : Exception(message);
public sealed class PasswordResetRequestNotFoundException(Guid requestId) : Exception($"Password reset request '{requestId}' was not found.");
public sealed class PasswordResetConflictException(string message) : Exception(message);
