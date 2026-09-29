namespace GhumoOdisha.Application.Cars;

/// <summary>
/// Business settings for car rentals ("CarRental" config section). Per-car prices live in the
/// database (<see cref="Domain.Entities.CarPricing"/>); these are the rules around them.
/// </summary>
public class CarRentalOptions
{
    public const string SectionName = "CarRental";

    /// <summary>Paid online to confirm a car booking. The rest of the fare is paid to the driver after the trip.</summary>
    public decimal BookingAmount { get; set; } = 99m;

    /// <summary>How long an unpaid booking holds the car before someone else can book it.</summary>
    public int PaymentHoldMinutes { get; set; } = 15;

    /// <summary>Gap kept free after every booking so the driver can get back / refuel before the next one.</summary>
    public int TurnaroundBufferMinutes { get; set; } = 60;

    /// <summary>Pre-filled night halt when a driver proposes pricing; drivers/admin can change it per car.</summary>
    public decimal DefaultNightHaltPrice { get; set; } = 400m;

    /// <summary>A pickup must be at least this far in the future, so the driver has time to plan.</summary>
    public int MinimumLeadMinutes { get; set; } = 120;

    public int MaxAdvanceBookingDays { get; set; } = 180;
    public int MinDurationHours { get; set; } = 4;
    public int MaxDurationHours { get; set; } = 720;
    public int MaxEstimatedKm { get; set; } = 10000;

    /// <summary>A single trip's odometer difference above this is rejected as a typing mistake.</summary>
    public int MaxTripKm { get; set; } = 10000;
}
