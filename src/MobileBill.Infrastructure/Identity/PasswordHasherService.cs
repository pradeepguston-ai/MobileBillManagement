using Microsoft.AspNetCore.Identity;
using MobileBill.Domain.Entities;

namespace MobileBill.Infrastructure.Identity;

public interface IPasswordHasherService
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}

public sealed class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<User> hasher = new();

    public string Hash(User user, string password) => hasher.HashPassword(user, password);

    public bool Verify(User user, string password) =>
        hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
