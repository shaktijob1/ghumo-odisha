namespace GhumoOdisha.Application.Auth;

public class OtpSettings
{
    public const string SectionName = "Otp";

    public int Length { get; set; } = 6;
    public int ExpiryMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
    public int ResendCooldownSeconds { get; set; } = 30;
    public int MaxRequestsPerHour { get; set; } = 5;

    // Skips the real Fast2SMS WhatsApp call and fixes every OTP to all-zeros so any number can
    // sign in during local development. Must stay false outside appsettings.Development.json.
    public bool DevBypassEnabled { get; set; } = false;
}
