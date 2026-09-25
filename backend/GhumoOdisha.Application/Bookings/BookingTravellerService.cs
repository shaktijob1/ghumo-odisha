using System.Text.RegularExpressions;
using GhumoOdisha.Application.Auth;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Bookings;

public interface IBookingTravellerService
{
    /// <summary>
    /// Replaces the booking's traveller list (one entry per seat, any subset of seats). A traveller
    /// with a phone number is linked to that phone's customer account — created silently (no OTP,
    /// no WhatsApp) if there isn't one — so the booking appears in their My Bookings when they log in.
    /// </summary>
    Task UpdateTravellersAsync(int bookingId, UpdateTravellersRequest request, CancellationToken cancellationToken = default);

    Task UpdateGenderCountsAsync(int bookingId, UpdateGenderCountsRequest request, CancellationToken cancellationToken = default);
}

public partial class BookingTravellerService(IGhumoOdishaDbContext db) : IBookingTravellerService
{
    public async Task UpdateTravellersAsync(int bookingId, UpdateTravellersRequest request, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Travellers)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        EnsureEditable(booking);

        var inputs = request.Travellers ?? [];
        var errors = new List<string>();
        if (inputs.Count > booking.NumberOfSeats)
        {
            errors.Add($"This booking has {booking.NumberOfSeats} seat(s) — you entered {inputs.Count} travellers.");
        }

        if (inputs.Select(t => t.SeatNumber).Distinct().Count() != inputs.Count)
        {
            errors.Add("Each seat can only have one traveller.");
        }

        var cleaned = new List<(TravellerInput Input, string Name, string? Phone, string? Aadhaar)>();
        foreach (var t in inputs.OrderBy(t => t.SeatNumber))
        {
            var label = $"Seat {t.SeatNumber}";
            if (t.SeatNumber < 1 || t.SeatNumber > booking.NumberOfSeats)
            {
                errors.Add($"{label}: seat number must be between 1 and {booking.NumberOfSeats}.");
            }

            var name = t.FullName?.Trim() ?? "";
            if (name.Length is < 2 or > 100)
            {
                errors.Add($"{label}: enter the traveller's full name.");
            }

            if (t.Age is < 0 or > 120)
            {
                errors.Add($"{label}: enter a valid age.");
            }

            string? aadhaar = string.IsNullOrWhiteSpace(t.AadhaarLast4) ? null : t.AadhaarLast4.Trim();
            if (aadhaar is not null && !AadhaarLast4Pattern().IsMatch(aadhaar))
            {
                errors.Add($"{label}: enter only the last 4 digits of the Aadhaar number.");
            }

            string? phone = null;
            if (!string.IsNullOrWhiteSpace(t.PhoneNumber))
            {
                phone = PhoneNumberNormalizer.Normalize(t.PhoneNumber);
                if (!PhonePattern().IsMatch(phone))
                {
                    errors.Add($"{label}: enter a valid 10-digit mobile number.");
                }
            }

            cleaned.Add((t, name, phone, aadhaar));
        }

        if (errors.Count > 0)
        {
            throw new ValidationAppException(errors);
        }

        var customersByPhone = await ResolveLinkedCustomersAsync(booking, cleaned.Select(c => c.Phone), cleaned, cancellationToken);

        var now = DateTime.UtcNow;
        var keptSeats = cleaned.Select(c => c.Input.SeatNumber).ToHashSet();
        foreach (var removed in booking.Travellers.Where(t => !keptSeats.Contains(t.SeatNumber)).ToList())
        {
            db.BookingTravellers.Remove(removed);
        }

        foreach (var (input, name, phone, aadhaar) in cleaned)
        {
            var traveller = booking.Travellers.FirstOrDefault(t => t.SeatNumber == input.SeatNumber);
            if (traveller is null)
            {
                traveller = new BookingTraveller { BookingId = booking.BookingId, SeatNumber = input.SeatNumber, CreatedAt = now };
                db.BookingTravellers.Add(traveller);
            }

            traveller.FullName = name;
            traveller.Gender = input.Gender;
            traveller.Age = input.Age;
            traveller.AadhaarLast4 = aadhaar;
            traveller.PhoneNumber = phone;
            traveller.UpdatedAt = now;

            if (phone is null)
            {
                traveller.LinkedCustomerId = null;
                traveller.LinkedCustomer = null;
            }
            else
            {
                traveller.LinkedCustomer = customersByPhone[phone];
            }
        }

        // Once every seat has a traveller with a gender, the counts follow the list exactly.
        var countNote = "";
        if (cleaned.Count == booking.NumberOfSeats && cleaned.All(c => c.Input.Gender.HasValue))
        {
            booking.MaleCount = cleaned.Count(c => c.Input.Gender == Gender.Male);
            booking.FemaleCount = cleaned.Count(c => c.Input.Gender == Gender.Female);
            countNote = $" · {booking.MaleCount} male, {booking.FemaleCount} female";
        }
        booking.UpdatedAt = now;

        BookingTimeline.Add(db, booking, BookingEventType.TravellersUpdated, "Traveller details updated",
            $"{cleaned.Count} of {booking.NumberOfSeats} seat(s) have traveller details{countNote}",
            BookingTimeline.Admin, at: now);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Someone else created a customer with one of these phones at the same moment — a retry links to it.
            throw new ConflictException("Someone else just updated these details. Please try again.");
        }
    }

    public async Task UpdateGenderCountsAsync(int bookingId, UpdateGenderCountsRequest request, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken)
            ?? throw new NotFoundException("Booking not found.");

        EnsureEditable(booking);

        if (request.MaleCount is < 0 || request.FemaleCount is < 0)
        {
            throw new ValidationAppException(["Counts can't be negative."]);
        }

        if ((request.MaleCount ?? 0) + (request.FemaleCount ?? 0) > booking.NumberOfSeats)
        {
            throw new ValidationAppException([$"Male + female can't be more than the {booking.NumberOfSeats} seat(s) booked."]);
        }

        var now = DateTime.UtcNow;
        booking.MaleCount = request.MaleCount;
        booking.FemaleCount = request.FemaleCount;
        booking.UpdatedAt = now;
        BookingTimeline.Add(db, booking, BookingEventType.GenderCountsUpdated, "Male / female count updated",
            $"{request.MaleCount ?? 0} male, {request.FemaleCount ?? 0} female of {booking.NumberOfSeats} seat(s)",
            BookingTimeline.Admin, visibleToCustomer: false, at: now);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureEditable(Booking booking)
    {
        if (booking.BookingStatus is BookingStatus.Rejected or BookingStatus.Cancelled)
        {
            throw new ConflictException("Traveller details can't be changed on a cancelled or rejected booking.");
        }
    }

    private async Task<Dictionary<string, Customer>> ResolveLinkedCustomersAsync(Booking booking, IEnumerable<string?> phones,
        List<(TravellerInput Input, string Name, string? Phone, string? Aadhaar)> travellers, CancellationToken cancellationToken)
    {
        var wanted = phones.OfType<string>().Distinct().ToList();
        var existing = await db.Customers
            .Where(c => wanted.Contains(c.PhoneNumber))
            .ToDictionaryAsync(c => c.PhoneNumber, cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var phone in wanted.Where(p => !existing.ContainsKey(p)))
        {
            // Unverified, no PIN — they become a normal account the first time they log in with this
            // number via WhatsApp OTP. Nothing is sent to them now.
            var customer = new Customer
            {
                Name = travellers.First(t => t.Phone == phone).Name,
                PhoneNumber = phone,
                IsVerified = false,
                FailedLoginAttempts = 0,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Customers.Add(customer);
            existing[phone] = customer;
        }

        return existing;
    }

    [GeneratedRegex("^[0-9]{4}$")]
    private static partial Regex AadhaarLast4Pattern();

    [GeneratedRegex("^[6-9][0-9]{9}$")]
    private static partial Regex PhonePattern();
}
