namespace GhumoOdisha.Application.Legal;

/// <summary>
/// The single canonical source of the Terms &amp; Conditions text — served to the customer's Terms
/// page and snapshotted verbatim into TermsAcceptance rows at booking time. Bump Version whenever
/// Text changes so past acceptances remain readable proof of exactly what an older customer agreed
/// to, even after the wording is later updated.
/// </summary>
public static class TermsAndConditionsContent
{
    public const string Version = "1.0";

    public const string Text = """
        Ghumo Odisha — Terms & Conditions

        By requesting or completing a booking with Ghumo Odisha, you agree to the following terms.

        1. Booking & Payment
        A booking request does not reserve your seats. Seats are confirmed only after our team
        contacts you, collects the advance payment, and marks the booking Confirmed. Prices,
        seat counts, and trip details are always subject to final confirmation by Ghumo Odisha,
        regardless of what is shown at the time of request.

        2. Conduct & Discipline
        All travellers must follow the instructions of the trip coordinator and driver at all
        times, including punctuality at pickup points and departure times. Consumption of alcohol
        or intoxicating substances during travel or organized activities is not permitted.
        Ghumo Odisha reserves the right to remove any traveller from the trip, without refund,
        if their behaviour endangers themselves, other travellers, staff, or the public, or if
        they repeatedly disregard safety instructions or the coordinator's directions.

        3. Safety
        Adventure activities, trekking, waterfalls, and coastal areas carry inherent risks.
        Travellers must wear seatbelts where fitted, follow all posted safety signage, and
        disclose any medical conditions that may affect their participation before the trip
        begins. Travellers participate in all activities at their own risk and are responsible
        for their own health, medication, and physical fitness for the itinerary chosen.

        4. Personal Belongings
        Ghumo Odisha, its coordinators, and drivers are not responsible for loss, theft, or
        damage to personal belongings during the trip. Travellers are responsible for the
        safekeeping of their own valuables at all times.

        5. Cancellations & Refunds
        Confirmed bookings may be cancelled up to 72 hours before the trip start date for a
        full refund of any amount paid online. Cancellations requested within 72 hours of the
        trip start date, or no-shows, are not eligible for a refund. Requested (unconfirmed)
        bookings may be withdrawn at any time with nothing to refund, since no payment is taken
        at the request stage.

        6. Itinerary Changes
        Itineraries may change due to weather, road conditions, local authority restrictions,
        or other circumstances beyond our control. Ghumo Odisha will make reasonable efforts to
        offer a comparable alternative but is not liable for costs arising from such changes.

        7. Liability
        Ghumo Odisha is not liable for injury, loss, delay, or damage arising from circumstances
        beyond its reasonable control, from a traveller's own negligence or non-disclosure of a
        medical condition, or from the acts of third-party service providers (hotels, vehicle
        operators, activity vendors) engaged for the trip.

        8. Photography & Media
        Photos and videos taken during the trip by Ghumo Odisha staff may be used for the
        company's promotional materials and social media. Travellers who do not wish to be
        included should inform the coordinator at the start of the trip.

        9. Governing Law
        These terms are governed by the laws of India, with courts in Odisha having jurisdiction
        over any dispute.

        By accepting these terms, you confirm that you have read, understood, and agree to be
        bound by them for the booking you are about to request.
        """;
}
