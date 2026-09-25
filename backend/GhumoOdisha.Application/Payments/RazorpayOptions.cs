namespace GhumoOdisha.Application.Payments;

public class RazorpayOptions
{
    public const string SectionName = "Razorpay";

    public string KeyId { get; set; } = string.Empty;
    public string KeySecret { get; set; } = string.Empty;

    // Skips the Razorpay checkout and signature check so "Pay now" confirms the booking straight
    // away during local development. The booking still goes through ConfirmBookingAsync (same
    // guarded seat deduction), just without a real charge. Turn on only via user-secrets on a dev
    // machine, never in a committed appsettings file — startup refuses to run with it on outside Development.
    public bool DevBypassEnabled { get; set; } = false;
}
