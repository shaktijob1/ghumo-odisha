using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// The person travelling on one seat of a booking, entered by the admin. Only the last four
/// digits of Aadhaar are ever stored. A traveller with a phone number is linked to that phone's
/// customer account (created silently if needed) so the booking shows in their My Bookings.
/// </summary>
public class BookingTraveller
{
    public int BookingTravellerId { get; set; }
    public int BookingId { get; set; }
    public int SeatNumber { get; set; }
    public string FullName { get; set; } = null!;
    public Gender? Gender { get; set; }
    public int? Age { get; set; }
    public string? AadhaarLast4 { get; set; }
    public string? PhoneNumber { get; set; }
    public int? LinkedCustomerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Booking Booking { get; set; } = null!;
    public Customer? LinkedCustomer { get; set; }
}
