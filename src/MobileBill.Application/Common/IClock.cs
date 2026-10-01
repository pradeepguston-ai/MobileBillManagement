namespace MobileBill.Application.Common;

public interface IClock { DateTimeOffset UtcNow { get; } }
