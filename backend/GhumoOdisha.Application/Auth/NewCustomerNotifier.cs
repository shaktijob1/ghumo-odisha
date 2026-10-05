using GhumoOdisha.Application.Contact;
using GhumoOdisha.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Auth;

/// <summary>Tells the organizer on WhatsApp when a new customer signs up.</summary>
public interface INewCustomerNotifier
{
    /// <summary>Never throws: a failed alert must not stop the customer signing in.</summary>
    Task NotifyAsync(Customer customer, string signUpMethod, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sends the alert to OrganizerContact:WhatsAppNumber through the approved OTP template (the same
/// two-variable template the "Booking Confirmed" admin notice uses), with the customer's name and
/// WhatsApp number — or their email when they signed up with Google and have no number yet.
/// </summary>
public class NewCustomerNotifier(
    IWhatsAppService whatsApp,
    IOptions<OrganizerContactOptions> organizerContact,
    ILogger<NewCustomerNotifier> logger) : INewCustomerNotifier
{
    /// <summary>Sign-in waits for the alert, so keep that wait short if WhatsApp is slow.</summary>
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);

    public async Task NotifyAsync(Customer customer, string signUpMethod, CancellationToken cancellationToken = default)
    {
        var adminNumber = organizerContact.Value.WhatsAppNumber;
        if (string.IsNullOrWhiteSpace(adminNumber))
        {
            return;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(SendTimeout);
            await whatsApp.SendTemplateAsync(adminNumber, "New Customer", Message(customer, signUpMethod), timeout.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "New-customer WhatsApp alert to the organizer failed — sign-in continues.");
        }
    }

    /// <summary>One line (WhatsApp template values can't contain line breaks).</summary>
    public static string Message(Customer customer, string signUpMethod)
    {
        var name = string.IsNullOrWhiteSpace(customer.Name) ? "name not given yet" : customer.Name.Trim();
        var contact = !string.IsNullOrWhiteSpace(customer.PhoneNumber)
            ? $"WhatsApp: {FormatPhone(customer.PhoneNumber)}"
            : !string.IsNullOrWhiteSpace(customer.Email) ? $"Email: {customer.Email} (no WhatsApp number yet)" : "no contact details yet";
        return $"New customer signed up via {signUpMethod}: {name}, {contact}";
    }

    /// <summary>Stored numbers are 10 digits ("9876543210") → "+91 98765 43210"; anything else is shown as stored.</summary>
    private static string FormatPhone(string phone) =>
        phone.Length == 10 && phone.All(char.IsDigit)
            ? $"+91 {phone[..5]} {phone[5..]}"
            : phone;
}
