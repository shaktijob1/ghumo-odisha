using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Crm;

/// <summary>Projections and checks shared by the CRM services (all run as single SQL queries).</summary>
internal static class CrmQueries
{
    public static IQueryable<CrmTripInterestDto> InterestDtos(IQueryable<CustomerTripInterest> interests) =>
        interests.Select(i => new CrmTripInterestDto(
            i.CustomerTripInterestId,
            i.CustomerId,
            i.Customer.Name,
            i.Customer.PhoneNumber,
            i.Customer.LeadSource,
            i.TripId,
            i.Trip.Title,
            i.TripDateSlotId,
            i.TripDateSlot != null ? i.TripDateSlot.StartDate : null,
            i.TripDateSlot != null ? i.TripDateSlot.EndDate : null,
            i.Travellers,
            i.Status,
            i.InterestedAt,
            i.LastContactAt,
            i.Customer.CrmFollowUps
                .Where(f => f.CustomerTripInterestId == i.CustomerTripInterestId && f.Status == CrmFollowUpStatus.Pending)
                .OrderBy(f => f.DueDate)
                .Select(f => (DateOnly?)f.DueDate)
                .FirstOrDefault(),
            i.Notes,
            i.ExpectedAmount,
            i.BookingId,
            i.Booking != null ? "GO-" + i.Booking.BookingNumber : null,
            i.Booking != null ? i.Booking.TotalAmount : null,
            i.Booking != null ? i.Booking.AdvanceAmount : null,
            i.LostReason));

    public static IQueryable<CrmFollowUpDto> FollowUpDtos(IQueryable<CrmFollowUp> followUps, DateOnly today) =>
        followUps.Select(f => new CrmFollowUpDto(
            f.CrmFollowUpId,
            f.CustomerId,
            f.Customer.Name,
            f.Customer.PhoneNumber,
            f.Customer.CrmStatus,
            f.CustomerTripInterestId,
            f.TripInterest != null ? f.TripInterest.Trip.Title : null,
            f.TripInterest != null && f.TripInterest.TripDateSlot != null ? f.TripInterest.TripDateSlot.StartDate : null,
            f.TripInterest != null ? f.TripInterest.Status : null,
            f.DueDate,
            f.Status == CrmFollowUpStatus.Pending && f.DueDate < today ? today.DayNumber - f.DueDate.DayNumber : 0,
            f.Note,
            f.Status,
            f.Outcome,
            f.CompletedAt,
            f.AssignedAdminUserId,
            f.AssignedAdminUser != null ? f.AssignedAdminUser.Username : null));

    public static IQueryable<CrmCommunicationDto> CommunicationDtos(IQueryable<CrmCommunication> communications) =>
        communications.Select(c => new CrmCommunicationDto(
            c.CrmCommunicationId,
            c.CustomerId,
            c.Customer.Name,
            c.Customer.PhoneNumber,
            c.CustomerTripInterestId,
            c.TripInterest != null ? c.TripInterest.Trip.Title : null,
            c.Channel,
            c.Status,
            c.Action,
            c.TemplateName,
            c.MessagePreview,
            c.AdminUser != null ? c.AdminUser.Username : null,
            c.CreatedAt,
            c.SentConfirmedAt));

    /// <summary>The trip exists and, when given, the departure belongs to it.</summary>
    public static async Task EnsureTripAndSlotAsync(IGhumoOdishaDbContext db, int tripId, int? tripDateSlotId, CancellationToken cancellationToken)
    {
        if (!await db.Trips.AnyAsync(t => t.TripId == tripId, cancellationToken))
        {
            throw new NotFoundException("Trip not found.");
        }

        if (tripDateSlotId is { } slotId && !await db.TripDateSlots.AnyAsync(s => s.TripDateSlotId == slotId && s.TripId == tripId, cancellationToken))
        {
            throw new ValidationAppException(["That departure doesn't belong to the chosen trip."]);
        }
    }

    /// <summary>A trip interest named in a request belongs to the same customer.</summary>
    public static async Task EnsureInterestBelongsAsync(IGhumoOdishaDbContext db, int customerId, int? interestId, CancellationToken cancellationToken)
    {
        if (interestId is { } id && !await db.CustomerTripInterests.AnyAsync(i => i.CustomerTripInterestId == id && i.CustomerId == customerId, cancellationToken))
        {
            throw new ValidationAppException(["That trip interest doesn't belong to this customer."]);
        }
    }
}
