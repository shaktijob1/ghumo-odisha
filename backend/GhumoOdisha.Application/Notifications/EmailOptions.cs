namespace GhumoOdisha.Application.Notifications;

/// <summary>
/// SMTP settings for outgoing email (Hostinger: smtp.hostinger.com, port 465 = SSL on connect).
/// <see cref="Password"/> is a secret — user-secrets locally, the Email__Password environment
/// variable in production, never a committed appsettings file. Empty <see cref="SmtpHost"/> or
/// <see cref="Password"/> = email not set up: email OTPs are refused and booking emails skipped.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 465;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Ghumo Odisha";

    /// <summary>Public site address, used for links in emails (e.g. "View my bookings").</summary>
    public string SiteUrl { get; set; } = "https://www.ghumoodisha.com";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SmtpHost) && !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(FromEmail);
}
