using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Application.Payments;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Crm;

public interface ICrmMessageService
{
    Task<CrmRenderedMessageDto> RenderAsync(RenderCrmMessageRequest request, CancellationToken cancellationToken = default);
    Task<CrmWhatsAppLinkDto> OpenWhatsAppAsync(OpenWhatsAppRequest request, int adminUserId, CancellationToken cancellationToken = default);
    Task MarkSentAsync(int communicationId, int adminUserId, CancellationToken cancellationToken = default);
    Task MarkNotSentAsync(int communicationId, CancellationToken cancellationToken = default);
    Task<int> LogAsync(LogCommunicationRequest request, int adminUserId, CancellationToken cancellationToken = default);
    Task<PagedResult<CrmCommunicationDto>> GetHistoryAsync(CrmCommunicationFilter filter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Manual WhatsApp messaging: the template is filled on the server from stored customer, trip and
/// booking data, the admin reviews it, and WhatsApp Web / the app opens with the text ready. Nothing
/// is ever sent automatically, and nothing is marked delivered or read — the CRM only records that
/// WhatsApp was opened, and that the admin confirmed they pressed Send if they say so.
/// </summary>
public class CrmMessageService(IGhumoOdishaDbContext db) : ICrmMessageService
{
    private const int MaxMessageLength = 4000;

    public async Task<CrmRenderedMessageDto> RenderAsync(RenderCrmMessageRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await LoadCustomerAsync(request.CustomerId, cancellationToken);

        string body;
        string? templateName = null;
        if (request.CrmWhatsAppTemplateId is { } templateId)
        {
            var template = await db.CrmWhatsAppTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.CrmWhatsAppTemplateId == templateId, cancellationToken)
                ?? throw new NotFoundException("Template not found.");
            body = template.Body;
            templateName = template.Name;
        }
        else
        {
            body = request.Body ?? throw new ValidationAppException(["Choose a template or type a message."]);
        }

        var values = await BuildValuesAsync(customer, request.CustomerTripInterestId, cancellationToken);
        var (message, missing) = CrmMessageRenderer.Render(body, values);

        return new CrmRenderedMessageDto(message.Trim(), templateName, customer.Name, CrmPhone.ForWhatsApp(customer.PhoneNumber), customer.WhatsAppAvailable, missing);
    }

    public async Task<CrmWhatsAppLinkDto> OpenWhatsAppAsync(OpenWhatsAppRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        var customer = await LoadCustomerAsync(request.CustomerId, cancellationToken);
        await CrmQueries.EnsureInterestBelongsAsync(db, customer.CustomerId, request.CustomerTripInterestId, cancellationToken);

        var waPhone = CrmPhone.ForWhatsApp(customer.PhoneNumber)
            ?? throw new ConflictException("This customer has no valid mobile number for WhatsApp. Add one to their profile first.");
        if (!customer.WhatsAppAvailable)
        {
            throw new ConflictException("This customer is marked as not on WhatsApp. Edit their profile if that has changed.");
        }

        var message = request.Message.Trim();
        if (message.Length == 0)
        {
            throw new ValidationAppException(["The message is empty."]);
        }

        if (message.Length > MaxMessageLength)
        {
            throw new ValidationAppException([$"The message is too long (max {MaxMessageLength} characters)."]);
        }

        string? templateName = null;
        if (request.CrmWhatsAppTemplateId is { } templateId)
        {
            templateName = await db.CrmWhatsAppTemplates.AsNoTracking()
                .Where(t => t.CrmWhatsAppTemplateId == templateId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Template not found.");
        }

        var log = new CrmCommunication
        {
            CustomerId = customer.CustomerId,
            CustomerTripInterestId = request.CustomerTripInterestId,
            CrmWhatsAppTemplateId = request.CrmWhatsAppTemplateId,
            TemplateName = templateName,
            Channel = CrmChannel.WhatsApp,
            Status = CrmMessageStatus.WhatsAppOpened,
            Action = "WhatsApp opened",
            MessagePreview = message,
            AdminUserId = adminUserId,
            CreatedAt = DateTime.UtcNow
        };
        db.CrmCommunications.Add(log);
        await db.SaveChangesAsync(cancellationToken);

        return new CrmWhatsAppLinkDto(log.CrmCommunicationId, CrmPhone.AppUrl(waPhone, message), CrmPhone.WebUrl(waPhone, message));
    }

    public async Task MarkSentAsync(int communicationId, int adminUserId, CancellationToken cancellationToken = default)
    {
        var log = await LoadWhatsAppLogAsync(communicationId, cancellationToken);
        if (log.Status == CrmMessageStatus.SentManually)
        {
            return;
        }

        var now = DateTime.UtcNow;
        log.Status = CrmMessageStatus.SentManually;
        log.SentConfirmedAt = now;
        await TouchLastContactAsync(log.CustomerId, log.CustomerTripInterestId, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkNotSentAsync(int communicationId, CancellationToken cancellationToken = default)
    {
        var log = await LoadWhatsAppLogAsync(communicationId, cancellationToken);
        if (log.Status == CrmMessageStatus.SentManually)
        {
            throw new ConflictException("This message was already confirmed as sent.");
        }

        log.Status = CrmMessageStatus.NotConfirmed;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> LogAsync(LogCommunicationRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        if (!await db.Customers.AnyAsync(c => c.CustomerId == request.CustomerId, cancellationToken))
        {
            throw new NotFoundException("Customer not found.");
        }

        await CrmQueries.EnsureInterestBelongsAsync(db, request.CustomerId, request.CustomerTripInterestId, cancellationToken);

        var now = DateTime.UtcNow;
        var log = new CrmCommunication
        {
            CustomerId = request.CustomerId,
            CustomerTripInterestId = request.CustomerTripInterestId,
            Channel = request.Channel,
            // Recorded after the fact by the admin, so it isn't a WhatsApp "opened / sent" state.
            Status = CrmMessageStatus.Logged,
            Action = request.Action.Trim(),
            MessagePreview = CrmHistory.Trim(request.Details, MaxMessageLength),
            AdminUserId = adminUserId,
            CreatedAt = now
        };
        db.CrmCommunications.Add(log);
        await TouchLastContactAsync(request.CustomerId, request.CustomerTripInterestId, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return log.CrmCommunicationId;
    }

    public async Task<PagedResult<CrmCommunicationDto>> GetHistoryAsync(CrmCommunicationFilter filter, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);
        var query = db.CrmCommunications.AsNoTracking();

        if (filter.CustomerId.HasValue)
        {
            query = query.Where(c => c.CustomerId == filter.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            var digits = new string(search.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("91"))
            {
                digits = digits[2..];
            }

            query = query.Where(c => c.Customer.Name.Contains(search)
                || (digits.Length >= 4 && c.Customer.PhoneNumber != null && c.Customer.PhoneNumber.Contains(digits)));
        }

        if (filter.TripId.HasValue)
        {
            query = query.Where(c => c.TripInterest != null && c.TripInterest.TripId == filter.TripId.Value);
        }

        if (filter.Channel.HasValue)
        {
            query = query.Where(c => c.Channel == filter.Channel.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(c => c.Status == filter.Status.Value);
        }

        if (filter.AdminUserId.HasValue)
        {
            query = query.Where(c => c.AdminUserId == filter.AdminUserId.Value);
        }

        if (filter.From.HasValue || filter.To.HasValue)
        {
            var range = CrmPeriod.Between(filter.From ?? new DateOnly(2000, 1, 1), filter.To ?? TripCalendar.Today());
            query = query.Where(c => c.CreatedAt >= range.FromUtc && c.CreatedAt < range.ToUtc);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await CrmQueries.CommunicationDtos(query.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<CrmCommunicationDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    /// <summary>
    /// Everything a template can show, from stored data. Without a chosen trip interest, the customer's
    /// latest open one is used (so "Hi {name}, about your {trip}…" works straight from their profile).
    /// </summary>
    private async Task<CrmMessageValues> BuildValuesAsync(Customer customer, int? interestId, CancellationToken cancellationToken)
    {
        var interests = db.CustomerTripInterests.AsNoTracking()
            .Include(i => i.Trip).ThenInclude(t => t.PickupPoints)
            .Include(i => i.TripDateSlot)
            .Include(i => i.Booking!).ThenInclude(b => b.PickupPoint)
            .Where(i => i.CustomerId == customer.CustomerId);

        CustomerTripInterest? interest;
        if (interestId is { } id)
        {
            interest = await interests.FirstOrDefaultAsync(i => i.CustomerTripInterestId == id, cancellationToken)
                ?? throw new ValidationAppException(["That trip interest doesn't belong to this customer."]);
        }
        else
        {
            interest = await interests
                .Where(i => i.Status != CrmStatus.Cancelled && i.Status != CrmStatus.NotInterested && i.Status != CrmStatus.Completed)
                .OrderByDescending(i => i.InterestedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var name = string.IsNullOrWhiteSpace(customer.Name) ? null : customer.Name.Trim();
        if (interest is null)
        {
            return new CrmMessageValues(name, null, null, null, null, null, null, null, null, null, null, null, null, null);
        }

        var trip = interest.Trip;
        var slot = interest.TripDateSlot;
        var booking = interest.Booking;
        var seats = booking?.NumberOfSeats ?? interest.Travellers;

        string? pickup;
        if (booking?.PickupPoint is { } chosen)
        {
            pickup = string.IsNullOrWhiteSpace(chosen.Time) ? chosen.Location : $"{chosen.Location} ({chosen.Time})";
        }
        else
        {
            var points = trip.PickupPoints.OrderBy(p => p.DisplayOrder).Select(p => p.Location).ToList();
            pickup = points.Count > 0 ? string.Join(", ", points) : null;
        }

        return new CrmMessageValues(
            name,
            trip.Title,
            slot?.StartDate,
            slot?.EndDate,
            // A booking keeps the price it was made at; otherwise today's price for the departure.
            booking?.AmountPerPerson ?? SlotPricing.PricePerPerson(trip, slot, TripCalendar.Today()),
            BookingPaymentService.PerSeatAdvanceAmount * seats,
            slot?.AvailableSeats,
            slot?.TotalSeats,
            seats,
            pickup,
            slot?.Vehicle,
            trip.IncludesStay ? "AC Stay" : null,
            booking?.RemainingAmount,
            booking is null ? null : booking.Reference);
    }

    private async Task<Customer> LoadCustomerAsync(int customerId, CancellationToken cancellationToken) =>
        await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
        ?? throw new NotFoundException("Customer not found.");

    private async Task<CrmCommunication> LoadWhatsAppLogAsync(int communicationId, CancellationToken cancellationToken)
    {
        var log = await db.CrmCommunications.FirstOrDefaultAsync(c => c.CrmCommunicationId == communicationId, cancellationToken)
            ?? throw new NotFoundException("Message not found.");
        if (log.Channel != CrmChannel.WhatsApp)
        {
            throw new ConflictException("Only WhatsApp messages can be marked as sent.");
        }

        return log;
    }

    private async Task TouchLastContactAsync(int customerId, int? interestId, DateTime now, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.FirstAsync(c => c.CustomerId == customerId, cancellationToken);
        customer.LastContactAt = now;
        if (interestId is { } id)
        {
            var interest = await db.CustomerTripInterests.FirstAsync(i => i.CustomerTripInterestId == id, cancellationToken);
            interest.LastContactAt = now;
        }
    }
}
