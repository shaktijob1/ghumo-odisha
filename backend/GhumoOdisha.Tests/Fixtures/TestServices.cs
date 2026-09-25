using GhumoOdisha.Application.Auth;
using GhumoOdisha.Infrastructure.Auth;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests.Fixtures;

public static class TestServices
{
    public static JwtSettings JwtSettings { get; } = new()
    {
        Issuer = "GhumoOdishaTests",
        Audience = "GhumoOdishaTestsClients",
        SigningKey = "test-only-signing-key-3f8a1c9e7b2d4f6a9c1e3b5d7f9a1c3e5b7d9f1a3c5e7b9d1f3a5c7e9b1d3f5a",
        CustomerAccessTokenLifetimeMinutes = 60,
        CustomerRefreshTokenLifetimeDays = 30,
        AdminTokenLifetimeHours = 8
    };

    public static OtpSettings OtpSettings { get; } = new()
    {
        Length = 6,
        ExpiryMinutes = 5,
        MaxAttempts = 3,
        ResendCooldownSeconds = 30,
        MaxRequestsPerHour = 5
    };

    public static CustomerAuthService CreateCustomerAuthService(
        GhumoOdisha.Infrastructure.Persistence.GhumoOdishaDbContext db,
        FakeWhatsAppService fakeWhatsApp,
        OtpSettings? otpSettings = null)
    {
        return new CustomerAuthService(
            db,
            new PinHasher(),
            new JwtTokenService(Options.Create(JwtSettings)),
            fakeWhatsApp,
            Options.Create(otpSettings ?? OtpSettings),
            Options.Create(JwtSettings),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<CustomerAuthService>.Instance);
    }
}
