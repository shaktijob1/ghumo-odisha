namespace GhumoOdisha.Application.Auth;

public class OtpSettings
{
    public const string SectionName = "Otp";

    public int Length { get; set; } = 6;
    public int ExpiryMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
    public int ResendCooldownSeconds { get; set; } = 30;
    public int MaxRequestsPerHour { get; set; } = 5;

    // Skips the real WhatsApp send and fixes every OTP to all-zeros so any number can
    // sign in during local development. Turn on only via user-secrets on a dev machine (never in a
    // committed appsettings file, which ends up in the published Docker image).
    public bool DevBypassEnabled { get; set; } = false;
}
