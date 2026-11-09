using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MobileBill.Application.Common;
using MobileBill.Application.Identity;
using MobileBill.Domain.Entities;
using MobileBill.Domain.Enums;
using MobileBill.Infrastructure.Identity;
using MobileBill.Infrastructure.Persistence;

namespace MobileBill.UnitTests.Identity;

public sealed class SignInThrottlingTests
{
    private const string Password = "correct-horse-battery";

    [Fact]
    public async Task An_account_is_refused_after_too_many_wrong_passwords_even_with_the_right_one_until_the_window_passes()
    {
        var (service, clock) = await CreateAsync();
        for (var attempt = 0; attempt < EfAuthService.MaxFailedSignIns; attempt++)
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync(new LoginRequest("user@concord.lk", "wrong-password"), default));

        var refusal = await Assert.ThrowsAsync<TooManySignInAttemptsException>(() => service.LoginAsync(new LoginRequest("user@concord.lk", Password), default));
        Assert.Contains("15 minutes", refusal.Message);

        clock.Advance(EfAuthService.FailedSignInWindow);
        Assert.Equal("user@concord.lk", (await service.LoginAsync(new LoginRequest("user@concord.lk", Password), default)).User.Email);
    }

    [Fact]
    public async Task A_successful_sign_in_clears_the_count_and_other_accounts_are_not_affected()
    {
        var (service, _) = await CreateAsync();
        for (var attempt = 0; attempt < EfAuthService.MaxFailedSignIns - 1; attempt++)
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync(new LoginRequest("user@concord.lk", "wrong-password"), default));
        await service.LoginAsync(new LoginRequest("USER@concord.lk ", Password), default);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync(new LoginRequest("user@concord.lk", "wrong-password"), default));
        await service.LoginAsync(new LoginRequest("user@concord.lk", Password), default);
    }

    [Fact]
    public async Task Unknown_emails_are_throttled_the_same_way_so_the_limit_does_not_reveal_registered_emails()
    {
        var (service, _) = await CreateAsync();
        for (var attempt = 0; attempt < EfAuthService.MaxFailedSignIns; attempt++)
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => service.LoginAsync(new LoginRequest("nobody@concord.lk", "wrong-password"), default));

        await Assert.ThrowsAsync<TooManySignInAttemptsException>(() => service.LoginAsync(new LoginRequest("nobody@concord.lk", "wrong-password"), default));
        await service.LoginAsync(new LoginRequest("user@concord.lk", Password), default);
    }

    private static async Task<(EfAuthService Service, TestClock Clock)> CreateAsync()
    {
        var db = new MobileBillDbContext(new DbContextOptionsBuilder<MobileBillDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var hasher = new PasswordHasherService();
        var user = new User { Email = "user@concord.lk", DisplayName = "User", PasswordHash = string.Empty, Role = UserRole.ITEngineer, Status = UserAccountStatus.Active };
        user.PasswordHash = hasher.Hash(user, Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var clock = new TestClock();
        return (new EfAuthService(db, hasher, new TestTokens(), clock, new MemoryCache(new MemoryCacheOptions())), clock);
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);
        public void Advance(TimeSpan by) => UtcNow += by;
    }

    private sealed class TestTokens : ITokenService
    {
        public (string Token, DateTimeOffset ExpiresAtUtc) CreateAccessToken(User user) => ("token", DateTimeOffset.MaxValue);
    }
}
