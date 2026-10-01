using MobileBill.Domain.Enums;

namespace MobileBill.Application.Common;

public interface ICurrentUserService { string UserId { get; } string DisplayName { get; } UserRole Role { get; } }
