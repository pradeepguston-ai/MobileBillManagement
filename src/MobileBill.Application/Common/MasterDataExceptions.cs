namespace MobileBill.Application.Common;

public sealed class MasterDataValidationException(string message) : Exception(message);

public sealed class MasterDataConflictException(string message) : Exception(message);

public sealed class MasterDataNotFoundException(string resource, Guid id)
    : Exception($"{resource} with id '{id}' was not found.");
