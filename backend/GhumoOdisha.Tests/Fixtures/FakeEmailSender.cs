using System.Text.RegularExpressions;
using GhumoOdisha.Application.Notifications;

namespace GhumoOdisha.Tests.Fixtures;

/// <summary>Captures outgoing email instead of hitting SMTP, so tests can read the code that was sent.</summary>
public class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];
    public bool IsConfigured { get; set; } = true;

    public EmailMessage? Last => Sent.LastOrDefault();

    /// <summary>The 6-digit code from the last OTP email's subject ("123456 is your Ghumo Odisha code").</summary>
    public string? LastOtp => Last is null ? null : Regex.Match(Last.Subject, @"^\d{4,8}").Value;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
