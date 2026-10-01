using MobileBill.Application.Common;
namespace MobileBill.Infrastructure.Billing;
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
