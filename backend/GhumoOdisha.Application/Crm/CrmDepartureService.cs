using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Application.Crm;

public interface ICrmDepartureService
{
    Task<PagedResult<CrmDepartureDto>> GetDeparturesAsync(CrmDepartureFilter filter, CancellationToken cancellationToken = default);
    Task<CrmDepartureDetailDto> GetDepartureAsync(int tripDateSlotId, CancellationToken cancellationToken = default);
    Task<int> CreateDepartureAsync(CreateCrmDepartureRequest request, CancellationToken cancellationToken = default);
    Task UpdateDetailsAsync(int tripDateSlotId, UpdateCrmDepartureDetailsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrmDepartureDto>> GetUpcomingAsync(int take, int? tripId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Departures are the website's own trip dates (TripDateSlots) — creating one here creates a real,
/// bookable date. Seat counts are the booking engine's; the CRM only reads them and adds lead counts.
/// </summary>
public class CrmDepartureService(IGhumoOdishaDbContext db, IOptions<CrmOptions> options) : ICrmDepartureService
{
    private readonly CrmOptions _options = options.Value;

    public async Task<PagedResult<CrmDepartureDto>> GetDeparturesAsync(CrmDepartureFilter filter, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);
        var today = TripCalendar.Today();
        var query = db.TripDateSlots.AsNoTracking();

        if (filter.TripId.HasValue)
        {
            query = query.Where(s => s.TripId == filter.TripId.Value);
        }

        if (filter.Year.HasValue || filter.Month.HasValue)
        {
            if (filter.Year is null)
            {
                throw new ValidationAppException(["Choose a year with the month."]);
            }

            var first = new DateOnly(filter.Year.Value, filter.Month ?? 1, 1);
            var last = filter.Month.HasValue ? first.AddMonths(1).AddDays(-1) : new DateOnly(filter.Year.Value, 12, 31);
            query = query.Where(s => s.StartDate >= first && s.StartDate <= last);
        }
        else if (!filter.IncludePast)
        {
            query = query.Where(s => s.EndDate >= today);
        }

        var total = await query.CountAsync(cancellationToken);
        var slots = await query
            .Include(s => s.Trip)
            .OrderBy(s => s.StartDate).ThenBy(s => s.Trip.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CrmDepartureDto> { Items = await ToDtosAsync(slots, cancellationToken), TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<IReadOnlyList<CrmDepartureDto>> GetUpcomingAsync(int take, int? tripId, CancellationToken cancellationToken = default)
    {
        var today = TripCalendar.Today();
        var slots = await db.TripDateSlots.AsNoTracking()
            .Include(s => s.Trip)
            .Where(s => s.StartDate >= today && s.Status == TripDateSlotStatus.Active && (tripId == null || s.TripId == tripId))
            .OrderBy(s => s.StartDate)
            .Take(Math.Clamp(take, 1, 50))
            .ToListAsync(cancellationToken);
        return await ToDtosAsync(slots, cancellationToken);
    }

    public async Task<CrmDepartureDetailDto> GetDepartureAsync(int tripDateSlotId, CancellationToken cancellationToken = default)
    {
        var slot = await db.TripDateSlots.AsNoTracking().Include(s => s.Trip)
            .FirstOrDefaultAsync(s => s.TripDateSlotId == tripDateSlotId, cancellationToken)
            ?? throw new NotFoundException("Departure not found.");

        var departure = (await ToDtosAsync([slot], cancellationToken))[0];

        var interests = await CrmQueries.InterestDtos(db.CustomerTripInterests.AsNoTracking()
                .Where(i => i.TripDateSlotId == tripDateSlotId)
                .OrderBy(i => i.Status)
                .ThenByDescending(i => i.LastContactAt ?? i.InterestedAt))
            .ToListAsync(cancellationToken);

        // Real bookings on this date — paid ones, and ones that were paid and later cancelled.
        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => b.TripDateSlotId == tripDateSlotId && (b.BookingStatus == BookingStatus.Confirmed || b.BookingStatus == BookingStatus.Completed
                     || (b.BookingStatus == BookingStatus.Cancelled && b.ConfirmedAt != null)))
            .OrderBy(b => b.BookingStatus).ThenByDescending(b => b.ConfirmedAt)
            .Select(b => new CrmDepartureBookingDto(
                b.BookingId,
                "GO-" + b.BookingNumber,
                b.CustomerId,
                b.Customer.Name,
                b.Customer.PhoneNumber,
                b.Customer.LeadSource,
                b.NumberOfSeats,
                b.BookingStatus,
                b.BookingSource,
                b.TotalAmount,
                b.AdvanceAmount,
                b.RemainingAmount,
                b.CreatedAt))
            .ToListAsync(cancellationToken);

        return new CrmDepartureDetailDto(departure, interests, bookings);
    }

    public async Task<int> CreateDepartureAsync(CreateCrmDepartureRequest request, CancellationToken cancellationToken = default)
    {
        var trip = await db.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.TripId == request.TripId, cancellationToken)
            ?? throw new NotFoundException("Trip not found.");

        if (request.StartDate < TripCalendar.Today())
        {
            throw new ValidationAppException(["The departure date can't be in the past."]);
        }

        ValidatePricing(trip, request.PricePerPerson, request.EarlyBirdPrice, request.EarlyBirdEndsOn, request.StartDate);
        ValidateMinimum(request.MinimumBookedSeats, request.TotalSeats);

        if (await db.TripDateSlots.AnyAsync(s => s.TripId == request.TripId && s.StartDate == request.StartDate, cancellationToken))
        {
            throw new ConflictException("This trip already has a departure on that date.");
        }

        var now = DateTime.UtcNow;
        var slot = new TripDateSlot
        {
            TripId = request.TripId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TotalSeats = request.TotalSeats,
            AvailableSeats = request.TotalSeats,
            Status = request.OpenForBooking ? TripDateSlotStatus.Active : TripDateSlotStatus.Inactive,
            Vehicle = CrmHistory.Trim(request.Vehicle, 100),
            MinimumBookedSeats = request.MinimumBookedSeats,
            PricePerPerson = request.PricePerPerson,
            EarlyBirdPrice = request.EarlyBirdPrice,
            EarlyBirdEndsOn = request.EarlyBirdPrice is null ? null : request.EarlyBirdEndsOn,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.TripDateSlots.Add(slot);
        await db.SaveChangesAsync(cancellationToken);
        return slot.TripDateSlotId;
    }

    public async Task UpdateDetailsAsync(int tripDateSlotId, UpdateCrmDepartureDetailsRequest request, CancellationToken cancellationToken = default)
    {
        var slot = await db.TripDateSlots.Include(s => s.Trip)
            .FirstOrDefaultAsync(s => s.TripDateSlotId == tripDateSlotId, cancellationToken)
            ?? throw new NotFoundException("Departure not found.");

        ValidatePricing(slot.Trip, request.PricePerPerson, request.EarlyBirdPrice, request.EarlyBirdEndsOn, slot.StartDate);
        ValidateMinimum(request.MinimumBookedSeats, slot.TotalSeats);

        // Prices only affect bookings made from now on: existing bookings keep the price they were made at.
        slot.Vehicle = CrmHistory.Trim(request.Vehicle, 100);
        slot.MinimumBookedSeats = request.MinimumBookedSeats;
        slot.PricePerPerson = request.PricePerPerson;
        slot.EarlyBirdPrice = request.EarlyBirdPrice;
        slot.EarlyBirdEndsOn = request.EarlyBirdPrice is null ? null : request.EarlyBirdEndsOn;
        slot.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void ValidatePricing(Trip trip, decimal? pricePerPerson, decimal? earlyBirdPrice, DateOnly? earlyBirdEndsOn, DateOnly startDate)
    {
        var errors = new List<string>();
        if (pricePerPerson is <= 0)
        {
            errors.Add("The departure price must be more than 0 (or leave it empty to use the trip's price).");
        }

        if (earlyBirdPrice is not null)
        {
            var regular = pricePerPerson ?? trip.AmountPerPerson;
            if (earlyBirdPrice <= 0 || earlyBirdPrice >= regular)
            {
                errors.Add($"The early-booking price must be more than 0 and less than the regular price of Rs {regular:N0}.");
            }

            if (earlyBirdEndsOn is null)
            {
                errors.Add("Choose the last day of the early-booking price.");
            }
            else if (earlyBirdEndsOn >= startDate)
            {
                errors.Add("Early booking must end before the departure date.");
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }
    }

    private static void ValidateMinimum(int? minimumBookedSeats, int totalSeats)
    {
        if (minimumBookedSeats is { } minimum && (minimum < 1 || minimum > totalSeats))
        {
            throw new ValidationAppException([$"Minimum bookings must be between 1 and the {totalSeats} seats on this departure."]);
        }
    }

    private async Task<IReadOnlyList<CrmDepartureDto>> ToDtosAsync(IReadOnlyList<TripDateSlot> slots, CancellationToken cancellationToken)
    {
        if (slots.Count == 0)
        {
            return [];
        }

        var ids = slots.Select(s => s.TripDateSlotId).ToList();
        var counts = await db.CustomerTripInterests.AsNoTracking()
            .Where(i => i.TripDateSlotId != null && ids.Contains(i.TripDateSlotId.Value))
            .GroupBy(i => new { SlotId = i.TripDateSlotId!.Value, i.Status })
            .Select(g => new { g.Key.SlotId, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var today = TripCalendar.Today();
        return slots.Select(s =>
        {
            int Count(CrmStatus status) => counts.Where(c => c.SlotId == s.TripDateSlotId && c.Status == status).Sum(c => c.Count);
            return new CrmDepartureDto(
                s.TripDateSlotId,
                s.TripId,
                s.Trip.Title,
                s.StartDate,
                s.EndDate,
                s.Status,
                s.Vehicle,
                SlotPricing.RegularPrice(s.Trip, s),
                s.PricePerPerson,
                s.EarlyBirdPrice,
                s.EarlyBirdEndsOn,
                SlotPricing.PricePerPerson(s.Trip, s, today),
                SlotPricing.IsEarlyBird(s, today),
                BookingPaymentService.PerSeatAdvanceAmount,
                s.TotalSeats,
                s.TotalSeats - s.AvailableSeats,
                s.AvailableSeats,
                s.MinimumBookedSeats,
                CrmDepartureRules.MinimumReached(s),
                CrmDepartureRules.StatusOf(s, today, _options),
                counts.Where(c => c.SlotId == s.TripDateSlotId).Sum(c => c.Count),
                Count(CrmStatus.Interested),
                Count(CrmStatus.HotLead),
                Count(CrmStatus.BookingPending),
                Count(CrmStatus.Booked) + Count(CrmStatus.Completed),
                Count(CrmStatus.Cancelled));
        }).ToList();
    }
}
