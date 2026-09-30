namespace GhumoOdisha.Application.Auth;

/// <summary>
/// Variables for the approved "Booking Confirmed" WhatsApp template, in template order
/// ({{1}}..{{9}}). All values are already formatted for display — the template supplies the
/// labels, emoji and the ₹ sign around them.
/// </summary>
public record BookingConfirmedWhatsAppMessage(
    string CustomerName,
    string TripTitle,
    string TravelDate,
    /// <summary>"2 Gents, 1 Lady" — or the seat count when the booking has no gents / ladies split.</summary>
    string Passengers,
    string Seats,
    string AmountPaid,
    string BookingReference,
    string PickupPoint,
    string ReportingTime)
{
    public IReadOnlyList<string> ToTemplateVariables() =>
        [CustomerName, TripTitle, TravelDate, Passengers, Seats, AmountPaid, BookingReference, PickupPoint, ReportingTime];
}
