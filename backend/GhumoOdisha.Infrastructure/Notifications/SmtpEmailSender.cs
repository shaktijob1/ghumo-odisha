using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Notifications;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace GhumoOdisha.Infrastructure.Notifications;

/// <summary>
/// Sends mail through an authenticated SMTP server (Hostinger). MailKit rather than
/// System.Net.Mail because port 465 needs SSL-on-connect, which SmtpClient can't do.
/// </summary>
public class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);
    private readonly EmailOptions _options = options.Value;

    public bool IsConfigured => _options.IsConfigured;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new EmailDeliveryException("Email isn't set up yet.");
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        mime.To.Add(new MailboxAddress(message.ToName ?? "", message.ToEmail));
        mime.Subject = message.Subject;

        var body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody };
        foreach (var attachment in message.Attachments ?? [])
        {
            body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }
        mime.Body = body.ToMessageBody();

        // 465 = implicit TLS; anything else (587) = STARTTLS.
        var security = _options.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
            await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, security, timeout.Token);
            await client.AuthenticateAsync(_options.Username, _options.Password, timeout.Token);
            await client.SendAsync(mime, timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Recipient is logged by domain only — the full address is personal data.
            var domain = message.ToEmail.Contains('@') ? message.ToEmail[(message.ToEmail.IndexOf('@') + 1)..] : "?";
            logger.LogError(ex, "SMTP send failed ({Subject}) to a recipient at {Domain}.", message.Subject, domain);
            throw new EmailDeliveryException();
        }
    }
}
