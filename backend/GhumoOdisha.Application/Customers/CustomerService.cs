using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Customers.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Customers;

public class CustomerService(IGhumoOdishaDbContext db) : ICustomerService
{
    private static readonly List<BookingStatus> FinanciallyActiveStatuses =
        [BookingStatus.Confirmed, BookingStatus.Completed];

    public async Task<CustomerProfileDto> GetProfileAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        return new CustomerProfileDto(
            customer.CustomerId, customer.Name, customer.PhoneNumber, customer.Email,
            customer.IsVerified, customer.CreatedAt, customer.LastLoginAt,
            customer.EmailVerified, customer.GoogleSubject is not null);
    }

    public async Task UpdateProfileAsync(int customerId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();

        // A Google-verified email is what Google sign-in matches on — it never changes by typing.
        if (customer.EmailVerified && email != customer.Email)
        {
            throw new ConflictException("Your email comes from your linked Google account and can't be edited here.");
        }

        customer.Name = request.Name;
        customer.Email = email;
        customer.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PagedResult<AdminCustomerListItemDto>> GetAdminCustomersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = db.Customers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                c.Name.Contains(search) ||
                (c.PhoneNumber != null && c.PhoneNumber.Contains(search)) ||
                (c.Email != null && c.Email.Contains(search)));
        }

        query = query.OrderByDescending(c => c.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var customers = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var customerIds = customers.Select(c => c.CustomerId).ToList();

        var aggregates = await db.Bookings
            .Where(b => customerIds.Contains(b.CustomerId) && FinanciallyActiveStatuses.Contains(b.BookingStatus))
            .GroupBy(b => b.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                TripCount = g.Select(b => b.TripId).Distinct().Count(),
                ConfirmedBookingCount = g.Count(b => b.BookingStatus == BookingStatus.Confirmed || b.BookingStatus == BookingStatus.Completed),
                TotalAmount = g.Sum(b => b.TotalAmount),
                AdvancePaid = g.Sum(b => b.AdvanceAmount),
                Remaining = g.Sum(b => b.RemainingAmount)
            })
            .ToDictionaryAsync(g => g.CustomerId, cancellationToken);

        var items = customers.Select(c =>
        {
            aggregates.TryGetValue(c.CustomerId, out var agg);
            return new AdminCustomerListItemDto(
                c.CustomerId, c.Name, c.PhoneNumber, c.Email,
                agg?.TripCount ?? 0, agg?.ConfirmedBookingCount ?? 0,
                agg?.TotalAmount ?? 0, agg?.AdvancePaid ?? 0, agg?.Remaining ?? 0);
        }).ToList();

        return new PagedResult<AdminCustomerListItemDto> { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };
    }

    public async Task<AdminCustomerDetailDto> GetAdminCustomerDetailAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
            ?? throw new NotFoundException("Customer not found.");

        var bookings = await db.Bookings.Where(b => b.CustomerId == customerId).ToListAsync(cancellationToken);
        var activeBookings = bookings.Where(b => FinanciallyActiveStatuses.Contains(b.BookingStatus)).ToList();

        return new AdminCustomerDetailDto(
            customer.CustomerId,
            customer.Name,
            customer.PhoneNumber,
            customer.Email,
            customer.IsVerified,
            customer.CreatedAt,
            customer.LastLoginAt,
            bookings.Count,
            activeBookings.Count(b => b.BookingStatus is BookingStatus.Confirmed or BookingStatus.Completed),
            activeBookings.Sum(b => b.TotalAmount),
            activeBookings.Sum(b => b.AdvanceAmount),
            activeBookings.Sum(b => b.RemainingAmount));
    }
}
