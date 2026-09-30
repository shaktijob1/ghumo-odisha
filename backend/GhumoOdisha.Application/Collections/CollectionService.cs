using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Trips.Dtos;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Collections;

public class CollectionService(IGhumoOdishaDbContext db, IImageStorage imageStorage) : ICollectionService
{
    private static readonly string[] AllowedImageContentTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;
    private const int MaxCaptionLength = 200;

    /// <summary>Bookings whose money is being collected: confirmed, plus completed trips that may still owe a balance.</summary>
    private IQueryable<Booking> Collectable() =>
        db.Bookings.AsNoTracking().Where(b => b.BookingStatus == BookingStatus.Confirmed || b.BookingStatus == BookingStatus.Completed);

    public async Task<IReadOnlyList<CollectionTripDto>> GetTripsAsync(CancellationToken cancellationToken = default)
    {
        // Confirmed/completed bookings always keep their date slot, so the slot join is safe here.
        var rows = await Collectable()
            .GroupBy(b => new { b.TripId, b.Trip.Title, SlotId = b.TripDateSlot!.TripDateSlotId, b.TripDateSlot.StartDate, b.TripDateSlot.EndDate })
            .Select(g => new
            {
                g.Key.TripId,
                g.Key.Title,
                g.Key.SlotId,
                g.Key.StartDate,
                g.Key.EndDate,
                Bookings = g.Count(),
                Remaining = g.Sum(b => b.RemainingAmount)
            })
            .ToListAsync(cancellationToken);

        // Trips with a departure still to come first (soonest on top — the one being run this week),
        // then past trips, most recent first.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return rows
            .GroupBy(r => new { r.TripId, r.Title })
            .Select(g => new CollectionTripDto(
                g.Key.TripId,
                g.Key.Title,
                g.Sum(r => r.Bookings),
                g.Sum(r => r.Remaining),
                g.OrderBy(r => r.StartDate)
                    .Select(r => new CollectionDepartureDto(r.SlotId, r.StartDate, r.EndDate, r.Bookings, r.Remaining))
                    .ToList()))
            .OrderBy(t => t.Departures.Any(d => d.EndDate >= today) ? 0 : 1)
            .ThenBy(t => t.Departures.Where(d => d.EndDate >= today).Select(d => d.StartDate).DefaultIfEmpty(DateOnly.MaxValue).Min())
            .ThenByDescending(t => t.Departures.Max(d => d.StartDate))
            .ToList();
    }

    public async Task<CollectionSheetDto> GetSheetAsync(int tripId, int? tripDateSlotId, CancellationToken cancellationToken = default)
    {
        var tripTitle = await db.Trips.AsNoTracking().Where(t => t.TripId == tripId).Select(t => t.Title).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Trip not found.");

        var query = Collectable().Where(b => b.TripId == tripId);
        if (tripDateSlotId.HasValue)
        {
            query = query.Where(b => b.TripDateSlotId == tripDateSlotId.Value);
        }

        var rows = await query
            .OrderBy(b => b.TripDateSlot!.StartDate).ThenBy(b => b.Customer.Name)
            .Select(b => new
            {
                b.BookingId,
                b.BookingNumber,
                CustomerName = b.Customer.Name,
                CustomerPhone = b.Customer.PhoneNumber,
                b.TripDateSlot!.StartDate,
                b.TripDateSlot.EndDate,
                b.NumberOfSeats,
                b.TotalAmount,
                b.AdvanceAmount,
                b.RemainingAmount,
                b.BookingStatus,
                b.PaymentStatus
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new CollectionBookingDto(
                r.BookingId,
                $"GO-{r.BookingNumber}",
                r.CustomerName,
                r.CustomerPhone,
                r.StartDate,
                r.EndDate,
                r.NumberOfSeats,
                r.TotalAmount,
                r.AdvanceAmount,
                r.RemainingAmount,
                r.BookingStatus,
                r.PaymentStatus))
            .ToList();

        var totals = new CollectionTotalsDto(
            items.Count,
            items.Sum(i => i.NumberOfSeats),
            items.Sum(i => i.TotalAmount),
            items.Sum(i => i.Paid),
            items.Sum(i => i.Remaining));

        return new CollectionSheetDto(tripId, tripTitle, tripDateSlotId, totals, items);
    }

    public async Task<PaymentQrDto?> GetPaymentQrAsync(CancellationToken cancellationToken = default)
    {
        var qr = await db.PaymentQrCodes.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return qr is null ? null : new PaymentQrDto(qr.ImageUrl, qr.Caption, qr.UpdatedAt);
    }

    public async Task<PaymentQrDto> SetPaymentQrAsync(UploadedImage image, string? caption, CancellationToken cancellationToken = default)
    {
        if (!AllowedImageContentTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationAppException(["Only JPG, PNG or WebP images are allowed."]);
        }

        if (image.Length <= 0 || image.Length > MaxImageSizeBytes)
        {
            throw new ValidationAppException(["Image must be no larger than 5 MB."]);
        }

        caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        if (caption is { Length: > MaxCaptionLength })
        {
            throw new ValidationAppException([$"Caption can be at most {MaxCaptionLength} characters."]);
        }

        var url = await imageStorage.SaveAsync(image.Content, image.FileName, image.ContentType, "payment-qr", cancellationToken);
        var now = DateTime.UtcNow;

        var existing = await db.PaymentQrCodes.FirstOrDefaultAsync(cancellationToken);
        string? oldUrl = null;
        if (existing is not null)
        {
            oldUrl = existing.ImageUrl;
            existing.ImageUrl = url;
            existing.Caption = caption;
            existing.UpdatedAt = now;
        }
        else
        {
            existing = new PaymentQrCode { ImageUrl = url, Caption = caption, UpdatedAt = now };
            db.PaymentQrCodes.Add(existing);
        }

        await db.SaveChangesAsync(cancellationToken);
        if (oldUrl is not null)
        {
            imageStorage.Delete(oldUrl);
        }

        return new PaymentQrDto(existing.ImageUrl, existing.Caption, existing.UpdatedAt);
    }

    public async Task RemovePaymentQrAsync(CancellationToken cancellationToken = default)
    {
        var existing = await db.PaymentQrCodes.FirstOrDefaultAsync(cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.PaymentQrCodes.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        imageStorage.Delete(existing.ImageUrl);
    }
}
