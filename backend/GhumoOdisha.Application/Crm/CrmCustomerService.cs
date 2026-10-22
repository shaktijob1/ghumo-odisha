using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Crm;

public interface ICrmCustomerService
{
    Task<PagedResult<CrmCustomerListItemDto>> GetCustomersAsync(CrmCustomerFilter filter, CancellationToken cancellationToken = default);
    Task<CrmCustomerDetailDto> GetCustomerAsync(int customerId, CancellationToken cancellationToken = default);
    Task<CrmDuplicateCheckDto> CheckPhoneAsync(string phoneNumber, int? exceptCustomerId = null, CancellationToken cancellationToken = default);
    Task<int> CreateCustomerAsync(CreateCrmCustomerRequest request, int adminUserId, CancellationToken cancellationToken = default);
    Task UpdateCustomerAsync(int customerId, UpdateCrmCustomerRequest request, int adminUserId, CancellationToken cancellationToken = default);
    Task ChangeStatusAsync(int customerId, ChangeCrmStatusRequest request, int adminUserId, CancellationToken cancellationToken = default);
    Task<CrmNoteDto> AddNoteAsync(int customerId, AddCrmNoteRequest request, int adminUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrmAdminUserDto>> GetAdminUsersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// CRM customers are the website's own Customers rows — never a second customer table. A mobile number
/// belongs to one customer only (main or alternate), so adding someone who already exists is refused
/// and the admin is pointed at the existing customer to add another trip interest instead.
/// </summary>
public class CrmCustomerService(IGhumoOdishaDbContext db) : ICrmCustomerService
{
    // A List, not an array: on .NET 10 an array's Contains binds to a span overload EF Core can't translate.
    private static readonly List<BookingStatus> BookedStatuses = [BookingStatus.Confirmed, BookingStatus.Completed];

    public async Task<PagedResult<CrmCustomerListItemDto>> GetCustomersAsync(CrmCustomerFilter filter, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = ApplySearch(query, filter.Search.Trim());
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(c => c.CrmStatus == filter.Status.Value);
        }

        if (filter.Source.HasValue)
        {
            query = query.Where(c => c.LeadSource == filter.Source.Value);
        }

        if (filter.AssignedAdminUserId.HasValue)
        {
            query = query.Where(c => c.AssignedAdminUserId == filter.AssignedAdminUserId.Value);
        }

        if (filter.TripDateSlotId.HasValue)
        {
            var slotId = filter.TripDateSlotId.Value;
            query = query.Where(c => c.TripInterests.Any(i => i.TripDateSlotId == slotId) || c.Bookings.Any(b => b.TripDateSlotId == slotId));
        }
        else if (filter.TripId.HasValue)
        {
            var tripId = filter.TripId.Value;
            query = query.Where(c => c.TripInterests.Any(i => i.TripId == tripId) || c.Bookings.Any(b => b.TripId == tripId));
        }

        if (CrmPeriod.FromMonth(filter.Year, filter.Month) is { } period)
        {
            query = query.Where(c => c.CreatedAt >= period.FromUtc && c.CreatedAt < period.ToUtc);
        }

        if (filter.ActiveOnly)
        {
            query = query.Where(c => c.TripInterests.Any() || c.Bookings.Any() || c.CrmNotes.Any() || c.CrmCommunications.Any() || c.CrmFollowUps.Any());
        }

        var total = await query.CountAsync(cancellationToken);
        var today = TripCalendar.Today();
        var items = await query
            .OrderByDescending(c => c.LastContactAt ?? c.CreatedAt)
            .ThenByDescending(c => c.CustomerId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CrmCustomerListItemDto(
                c.CustomerId,
                c.Name,
                c.PhoneNumber,
                c.Email,
                c.City,
                c.LeadSource,
                c.CrmStatus,
                c.CreatedAt,
                c.LastContactAt,
                c.CrmFollowUps.Where(f => f.Status == CrmFollowUpStatus.Pending).OrderBy(f => f.DueDate).Select(f => (DateOnly?)f.DueDate).FirstOrDefault(),
                c.AssignedAdminUserId,
                c.AssignedAdminUser != null ? c.AssignedAdminUser.Username : null,
                c.TripInterests.Count,
                c.Bookings.Count(b => BookedStatuses.Contains(b.BookingStatus)),
                c.Bookings.Count(b => b.BookingStatus == BookingStatus.Completed),
                c.Bookings.Where(b => BookedStatuses.Contains(b.BookingStatus)).Sum(b => (decimal?)b.AdvanceAmount) ?? 0m,
                c.IsVerified))
            .ToListAsync(cancellationToken);

        return new PagedResult<CrmCustomerListItemDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    /// <summary>Name, mobile (main or alternate), email, booking ID ("GO-123456" or the number) or a trip they enquired about / booked.</summary>
    private static IQueryable<Customer> ApplySearch(IQueryable<Customer> query, string search)
    {
        var digits = new string(search.Where(char.IsDigit).ToArray());
        var bookingNumber = search.StartsWith("GO-", StringComparison.OrdinalIgnoreCase) && int.TryParse(search[3..], out var n) ? n
            : digits.Length is >= 4 and <= 9 && digits.Length == search.Length && int.TryParse(digits, out var m) ? m
            : (int?)null;
        var phoneDigits = digits.Length >= 4 ? (digits.Length == 12 && digits.StartsWith("91") ? digits[2..] : digits) : null;

        return query.Where(c =>
            c.Name.Contains(search)
            || (c.Email != null && c.Email.Contains(search))
            || (phoneDigits != null && ((c.PhoneNumber != null && c.PhoneNumber.Contains(phoneDigits)) || (c.AlternatePhoneNumber != null && c.AlternatePhoneNumber.Contains(phoneDigits))))
            || (bookingNumber != null && c.Bookings.Any(b => b.BookingNumber == bookingNumber))
            || c.TripInterests.Any(i => i.Trip.Title.Contains(search))
            || c.Bookings.Any(b => b.Trip.Title.Contains(search)));
    }

    public async Task<CrmCustomerDetailDto> GetCustomerAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.AsNoTracking()
            .Include(c => c.AssignedAdminUser)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        var interests = await CrmQueries.InterestDtos(db.CustomerTripInterests.AsNoTracking()
                .Where(i => i.CustomerId == customerId)
                .OrderByDescending(i => i.InterestedAt))
            .ToListAsync(cancellationToken);

        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => b.CustomerId == customerId && b.BookingStatus != BookingStatus.AwaitingPayment && b.BookingStatus != BookingStatus.Pending)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new CrmCustomerBookingDto(
                b.BookingId,
                "GO-" + b.BookingNumber,
                b.TripId,
                b.Trip.Title,
                b.TripDateSlot != null ? b.TripDateSlot.StartDate : b.RemovedSlotStartDate ?? default,
                b.NumberOfSeats,
                b.BookingStatus,
                b.BookingSource,
                b.TotalAmount,
                b.AdvanceAmount,
                b.RemainingAmount,
                b.CreatedAt))
            .ToListAsync(cancellationToken);

        var followUps = await CrmQueries.FollowUpDtos(db.CrmFollowUps.AsNoTracking()
                .Where(f => f.CustomerId == customerId)
                .OrderBy(f => f.Status).ThenBy(f => f.DueDate)
                .Take(50), TripCalendar.Today())
            .ToListAsync(cancellationToken);

        var notes = await db.CrmNotes.AsNoTracking()
            .Where(n => n.CustomerId == customerId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new CrmNoteDto(n.CrmNoteId, n.Text, n.CustomerTripInterestId, n.AdminUser != null ? n.AdminUser.Username : null, n.CreatedAt))
            .ToListAsync(cancellationToken);

        var communications = await CrmQueries.CommunicationDtos(db.CrmCommunications.AsNoTracking()
                .Where(c => c.CustomerId == customerId)
                .OrderByDescending(c => c.CreatedAt)
                .Take(50))
            .ToListAsync(cancellationToken);

        var history = await db.CrmStatusChanges.AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .Select(s => new CrmStatusChangeDto(
                s.CrmStatusChangeId,
                s.CustomerTripInterestId,
                s.TripInterest != null ? s.TripInterest.Trip.Title : null,
                s.FromStatus,
                s.ToStatus,
                s.Reason,
                s.AdminUser != null ? s.AdminUser.Username : null,
                s.CreatedAt))
            .ToListAsync(cancellationToken);

        var booked = bookings.Where(b => BookedStatuses.Contains(b.Status)).ToList();
        var nextFollowUp = followUps.Where(f => f.Status == CrmFollowUpStatus.Pending).Select(f => (DateOnly?)f.DueDate).FirstOrDefault();

        return new CrmCustomerDetailDto(
            customer.CustomerId,
            customer.Name,
            customer.PhoneNumber,
            customer.AlternatePhoneNumber,
            customer.Email,
            customer.WhatsAppAvailable,
            customer.City,
            customer.LeadSource,
            customer.CrmStatus,
            customer.CrmSummary,
            customer.CreatedAt,
            customer.LastContactAt,
            nextFollowUp,
            customer.AssignedAdminUserId,
            customer.AssignedAdminUser?.Username,
            customer.IsVerified,
            booked.Count,
            booked.Count(b => b.Status == BookingStatus.Completed),
            booked.Sum(b => b.Paid),
            interests,
            bookings,
            followUps,
            notes,
            communications,
            history);
    }

    public async Task<CrmDuplicateCheckDto> CheckPhoneAsync(string phoneNumber, int? exceptCustomerId = null, CancellationToken cancellationToken = default)
    {
        var phone = CrmPhone.Normalize(phoneNumber)
            ?? throw new ValidationAppException(["Enter a valid 10-digit Indian mobile number."]);

        var match = await db.Customers.AsNoTracking()
            .Where(c => (c.PhoneNumber == phone || c.AlternatePhoneNumber == phone) && (exceptCustomerId == null || c.CustomerId != exceptCustomerId))
            .Select(c => new
            {
                c.CustomerId,
                c.Name,
                c.PhoneNumber,
                c.CrmStatus,
                Interests = c.TripInterests.Count,
                Bookings = c.Bookings.Count(b => BookedStatuses.Contains(b.BookingStatus)),
                MatchedAlternate = c.PhoneNumber != phone
            })
            .FirstOrDefaultAsync(cancellationToken);

        return match is null
            ? new CrmDuplicateCheckDto(false, null, null, null, null, 0, 0, false)
            : new CrmDuplicateCheckDto(true, match.CustomerId, match.Name, match.PhoneNumber, match.CrmStatus, match.Interests, match.Bookings, match.MatchedAlternate);
    }

    public async Task<int> CreateCustomerAsync(CreateCrmCustomerRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        var phone = CrmPhone.Normalize(request.PhoneNumber)
            ?? throw new ValidationAppException(["Enter a valid 10-digit Indian mobile number."]);
        var alternate = NormalizeAlternate(request.AlternatePhoneNumber, phone);

        await EnsurePhonesFreeAsync(phone, alternate, null, cancellationToken);
        await EnsureAdminExistsAsync(request.AssignedAdminUserId, cancellationToken);
        if (request.Status == CrmStatus.RepeatCustomer)
        {
            throw new ValidationAppException(["A new customer can't start as a repeat customer."]);
        }

        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Name = request.Name.Trim(),
            PhoneNumber = phone,
            AlternatePhoneNumber = alternate,
            Email = CrmHistory.Trim(request.Email, 200),
            WhatsAppAvailable = request.WhatsAppAvailable,
            City = CrmHistory.Trim(request.City, 100),
            LeadSource = request.LeadSource,
            CrmStatus = request.Status,
            CrmSummary = CrmHistory.Trim(request.Summary, 2000),
            AssignedAdminUserId = request.AssignedAdminUserId,
            // Not signed in yet: the account becomes theirs the first time they log in with this number.
            IsVerified = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Customers.Add(customer);
        db.CrmStatusChanges.Add(new CrmStatusChange { Customer = customer, ToStatus = request.Status, Reason = "Added in CRM", AdminUserId = adminUserId, CreatedAt = now });

        CustomerTripInterest? interest = null;
        if (request.Interest is { } input)
        {
            await CrmQueries.EnsureTripAndSlotAsync(db, input.TripId, input.TripDateSlotId, cancellationToken);
            interest = new CustomerTripInterest
            {
                Customer = customer,
                TripId = input.TripId,
                TripDateSlotId = input.TripDateSlotId,
                Travellers = input.Travellers,
                Status = input.Status,
                InterestedAt = now,
                Notes = CrmHistory.Trim(input.Notes, 2000),
                ExpectedAmount = input.ExpectedAmount,
                CreatedByAdminUserId = adminUserId,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.CustomerTripInterests.Add(interest);
            db.CrmStatusChanges.Add(new CrmStatusChange { Customer = customer, TripInterest = interest, ToStatus = input.Status, Reason = "Trip interest added", AdminUserId = adminUserId, CreatedAt = now });
        }

        if (request.NextFollowUpDate is { } due)
        {
            db.CrmFollowUps.Add(new CrmFollowUp
            {
                Customer = customer,
                TripInterest = interest,
                DueDate = due,
                Note = CrmHistory.Trim(request.FollowUpNote, 1000),
                Status = CrmFollowUpStatus.Pending,
                AssignedAdminUserId = request.AssignedAdminUserId ?? adminUserId,
                CreatedByAdminUserId = adminUserId,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Someone added (or signed up with) the same number a moment ago — the unique index caught it.
            throw new ConflictException("Customer already exists with this mobile number.");
        }

        return customer.CustomerId;
    }

    public async Task UpdateCustomerAsync(int customerId, UpdateCrmCustomerRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        var phone = CrmPhone.Normalize(request.PhoneNumber)
            ?? throw new ValidationAppException(["Enter a valid 10-digit Indian mobile number."]);
        var alternate = NormalizeAlternate(request.AlternatePhoneNumber, phone);

        // The main number is a registered customer's WhatsApp login — the CRM can't move it to another number.
        if (customer.IsVerified && customer.PhoneNumber != phone)
        {
            throw new ConflictException("This customer signs in with their mobile number, so it can't be changed here. Add the new number as the alternate number instead.");
        }

        await EnsurePhonesFreeAsync(phone, alternate, customerId, cancellationToken);
        await EnsureAdminExistsAsync(request.AssignedAdminUserId, cancellationToken);

        customer.Name = request.Name.Trim();
        customer.PhoneNumber = phone;
        customer.AlternatePhoneNumber = alternate;
        // A Google-verified email is the customer's sign-in identity; only an unverified one can be edited.
        if (!customer.EmailVerified)
        {
            customer.Email = CrmHistory.Trim(request.Email, 200);
        }

        customer.WhatsAppAvailable = request.WhatsAppAvailable;
        customer.City = CrmHistory.Trim(request.City, 100);
        customer.LeadSource = request.LeadSource;
        customer.CrmSummary = CrmHistory.Trim(request.Summary, 2000);
        customer.AssignedAdminUserId = request.AssignedAdminUserId;
        customer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeStatusAsync(int customerId, ChangeCrmStatusRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        CrmHistory.SetCustomerStatus(db, customer, request.Status, request.Reason, adminUserId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CrmNoteDto> AddNoteAsync(int customerId, AddCrmNoteRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        if (!await db.Customers.AnyAsync(c => c.CustomerId == customerId, cancellationToken))
        {
            throw new NotFoundException("Customer not found.");
        }

        await CrmQueries.EnsureInterestBelongsAsync(db, customerId, request.CustomerTripInterestId, cancellationToken);

        var note = new CrmNote
        {
            CustomerId = customerId,
            CustomerTripInterestId = request.CustomerTripInterestId,
            Text = request.Text.Trim(),
            AdminUserId = adminUserId,
            CreatedAt = DateTime.UtcNow
        };
        db.CrmNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);

        var adminName = await db.AdminUsers.Where(a => a.AdminUserId == adminUserId).Select(a => a.Username).FirstOrDefaultAsync(cancellationToken);
        return new CrmNoteDto(note.CrmNoteId, note.Text, note.CustomerTripInterestId, adminName, note.CreatedAt);
    }

    public async Task<IReadOnlyList<CrmAdminUserDto>> GetAdminUsersAsync(CancellationToken cancellationToken = default) =>
        await db.AdminUsers.AsNoTracking()
            .Where(a => a.Role == "Admin")
            .OrderBy(a => a.Username)
            .Select(a => new CrmAdminUserDto(a.AdminUserId, a.Username))
            .ToListAsync(cancellationToken);

    private static string? NormalizeAlternate(string? alternate, string phone)
    {
        if (string.IsNullOrWhiteSpace(alternate))
        {
            return null;
        }

        var normalized = CrmPhone.Normalize(alternate)
            ?? throw new ValidationAppException(["Enter a valid 10-digit alternate mobile number, or leave it empty."]);
        return normalized == phone ? null : normalized;
    }

    private async Task EnsurePhonesFreeAsync(string phone, string? alternate, int? exceptCustomerId, CancellationToken cancellationToken)
    {
        var numbers = alternate is null ? new List<string> { phone } : new List<string> { phone, alternate };
        var taken = await db.Customers.AsNoTracking()
            .Where(c => (exceptCustomerId == null || c.CustomerId != exceptCustomerId)
                     && ((c.PhoneNumber != null && numbers.Contains(c.PhoneNumber)) || (c.AlternatePhoneNumber != null && numbers.Contains(c.AlternatePhoneNumber))))
            .Select(c => new { c.CustomerId, c.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (taken is not null)
        {
            var name = string.IsNullOrWhiteSpace(taken.Name) ? $"customer #{taken.CustomerId}" : taken.Name;
            throw new ConflictException($"Customer already exists: {name} (#{taken.CustomerId}) has this mobile number. Open their profile and add another trip interest instead.");
        }
    }

    private async Task EnsureAdminExistsAsync(int? adminUserId, CancellationToken cancellationToken)
    {
        if (adminUserId is { } id && !await db.AdminUsers.AnyAsync(a => a.AdminUserId == id && a.Role == "Admin", cancellationToken))
        {
            throw new ValidationAppException(["Choose an admin user to assign this customer to."]);
        }
    }
}
