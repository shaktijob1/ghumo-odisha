namespace GhumoOdisha.Domain.Entities;

/// <summary>
/// What actually happened on the road: odometer readings, times and (optional) GPS at start and end.
/// Actual km = end − start odometer. Once CompletedAt is set the driver can't change anything here;
/// only an admin fare correction can, and that is audited.
/// </summary>
public class CarTripExecution
{
    public int CarTripExecutionId { get; set; }
    public int CarBookingId { get; set; }
    public int DriverId { get; set; }
    public int CarId { get; set; }

    public DateTime StartedAt { get; set; }
    public int StartOdometerKm { get; set; }
    public decimal? StartLatitude { get; set; }
    public decimal? StartLongitude { get; set; }

    public DateTime? EndedAt { get; set; }
    public int? EndOdometerKm { get; set; }
    public decimal? EndLatitude { get; set; }
    public decimal? EndLongitude { get; set; }
    public int? ActualKm { get; set; }
    /// <summary>Nights the driver actually stayed out — entered by the driver, capped by the calendar.</summary>
    public int? NightHalts { get; set; }
    public DateTime? CompletedAt { get; set; }

    public CarBooking CarBooking { get; set; } = null!;
}
