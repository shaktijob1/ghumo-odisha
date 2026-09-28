namespace GhumoOdisha.Application.Legal;

/// <summary>
/// The single canonical source of the Terms &amp; Conditions text — served to the customer's Terms
/// page and snapshotted verbatim into TermsAcceptance rows at booking time. Bump Version whenever
/// Text changes so past acceptances remain readable proof of exactly what an older customer agreed
/// to, even after the wording is later updated.
/// </summary>
public static class TermsAndConditionsContent
{
    public const string Version = "1.2";

    public const string Text = """
        Ghumo Odisha — Trip Terms & Conditions

        By requesting or completing a booking with Ghumo Odisha, you agree to the following terms
        on behalf of yourself and every traveller included in your booking.

        1. Booking & Payment
        A seat is held only once the booking amount of ₹99 per seat has been paid and the booking
        is confirmed by Ghumo Odisha. The remaining trip amount must be paid before departure as
        communicated by our team. Prices, seat availability, and trip details are always subject
        to final confirmation by Ghumo Odisha, regardless of what is shown at the time of request.

        2. Non-Refundable Booking Amount
        The booking amount of ₹99 per seat is non-refundable under all circumstances, including
        cancellation by the traveller, no-show, missed pickup, or removal from the trip.

        3. Cancellations & Refunds
        Confirmed bookings may be cancelled up to 72 hours before the trip start date for a refund
        of the amount paid, excluding the non-refundable booking amount. Cancellations requested
        within 72 hours of the trip start date, no-shows, and missed pickups are not eligible for
        a refund. If Ghumo Odisha cancels a trip, you will be offered another date or a full refund
        of the amount paid, including the booking amount. Approved refunds are processed to the
        original payment method or by bank/UPI transfer within 7–10 working days.

        4. Conduct & Discipline
        All travellers must follow the instructions of the trip coordinator and driver at all
        times. Misbehaviour, indiscipline, harassment of fellow travellers, staff, or locals,
        consumption of alcohol or intoxicating substances during travel or organized activities,
        or repeatedly disregarding safety instructions will not be tolerated. Ghumo Odisha may
        remove such a traveller from the trip at any point, including midway, without any refund.
        The removed traveller's onward or return travel is at their own cost and responsibility.

        5. Safety, Health & Injuries
        Adventure activities, trekking, waterfalls, swimming, and coastal areas carry inherent
        risks. Travellers participate in all activities at their own risk. Any injury, illness,
        or accident suffered during the trip is the traveller's own responsibility, and Ghumo
        Odisha, its coordinators, and drivers are not liable for it. Travellers must disclose any
        medical condition that may affect their participation before the trip begins, carry
        their own medication, wear seatbelts where fitted, and swim or enter water only where
        the coordinator permits.

        6. Rooms & Vehicles
        Photos of rooms, stays, and vehicles are for reference only. The actual room or vehicle
        provided may differ from the photos due to availability, but will be of a similar
        standard. Rooms are allotted on a sharing basis and vehicle seats are assigned by the trip
        coordinator.

        7. Punctuality & Pickup
        Travellers must reach the pickup point on time. The group cannot wait beyond a reasonable
        time, and a traveller who misses the pickup or a scheduled departure is treated as a
        no-show with no refund.

        8. Identity & Travellers
        Every traveller must carry a valid government-issued photo ID (such as Aadhaar, Voter ID,
        Driving Licence, or Passport) for the entire trip. Travellers under 18 must be accompanied
        by a parent or guardian, who is responsible for them throughout the trip.

        9. Damage to Property
        Travellers are responsible for any damage they cause to vehicles, rooms, hotel property,
        or third-party property during the trip, and must pay for it directly.

        10. Personal Belongings
        Ghumo Odisha, its coordinators, and drivers are not responsible for loss, theft, or
        damage to personal belongings during the trip. Travellers are responsible for the
        safekeeping of their own valuables at all times.

        11. Itinerary Changes
        Itineraries may change due to weather, road conditions, local authority restrictions, or
        other circumstances beyond our control. Ghumo Odisha will make reasonable efforts to offer
        a comparable alternative but is not liable for costs arising from such changes, and no
        refund is due for activities missed for these reasons.

        12. Liability
        Ghumo Odisha is not liable for injury, loss, delay, or damage arising from circumstances
        beyond its reasonable control, from a traveller's own negligence or non-disclosure of a
        medical condition, or from the acts of third-party service providers (hotels, vehicle
        operators, activity vendors) engaged for the trip.

        13. Photography & Media
        Photos and videos taken during the trip by Ghumo Odisha staff may be used for the
        company's promotional materials and social media. Travellers who do not wish to be
        included should inform the coordinator at the start of the trip.

        14. Governing Law
        These terms are governed by the laws of India, with courts in Odisha having jurisdiction
        over any dispute.

        By accepting these terms, you confirm that you have read, understood, and agree to be
        bound by them for the booking you are about to make.
        """;
}
