namespace GhumoOdisha.Application.Notifications;

public record EmailAttachment(string FileName, string ContentType, byte[] Content);

public record EmailMessage(
    string ToEmail,
    string? ToName,
    string Subject,
    string HtmlBody,
    string TextBody,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public interface IEmailSender
{
    /// <summary>False when SMTP isn't configured — callers skip or refuse email features.</summary>
    bool IsConfigured { get; }

    /// <summary>Sends one email. Throws <see cref="Exceptions.EmailDeliveryException"/> if the SMTP send fails.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
