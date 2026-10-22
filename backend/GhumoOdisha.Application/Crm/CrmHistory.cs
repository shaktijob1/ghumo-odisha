using GhumoOdisha.Application.Common;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;

namespace GhumoOdisha.Application.Crm;

/// <summary>
/// Stage changes for CRM customers and trip interests, always written with a history row.
/// A trip interest's new stage becomes the customer's stage too (the latest activity wins), except
/// that a repeat customer stays a repeat customer when another trip is booked or completed, and a
/// new enquiry doesn't move a customer back to "New lead".
/// </summary>
internal static class CrmHistory
{
    public static void SetCustomerStatus(IGhumoOdishaDbContext db, Customer customer, CrmStatus status, string? reason, int? adminUserId, DateTime now)
    {
        if (customer.CrmStatus == status)
        {
            return;
        }

        db.CrmStatusChanges.Add(new CrmStatusChange
        {
            CustomerId = customer.CustomerId,
            FromStatus = customer.CrmStatus,
            ToStatus = status,
            Reason = Trim(reason, 500),
            AdminUserId = adminUserId,
            CreatedAt = now
        });
        customer.CrmStatus = status;
        customer.UpdatedAt = now;
    }

    public static void SetInterestStatus(IGhumoOdishaDbContext db, Customer customer, CustomerTripInterest interest, CrmStatus status, string? reason, int? adminUserId, DateTime now)
    {
        if (status == CrmStatus.RepeatCustomer)
        {
            throw new Exceptions.ValidationAppException(["\"Repeat customer\" applies to a customer, not to one trip."]);
        }

        if (interest.Status != status)
        {
            db.CrmStatusChanges.Add(new CrmStatusChange
            {
                CustomerId = customer.CustomerId,
                TripInterest = interest,
                FromStatus = interest.CustomerTripInterestId == 0 ? null : interest.Status,
                ToStatus = status,
                Reason = Trim(reason, 500),
                AdminUserId = adminUserId,
                CreatedAt = now
            });
            interest.Status = status;
            interest.UpdatedAt = now;
        }

        var keepRepeat = customer.CrmStatus == CrmStatus.RepeatCustomer && status is CrmStatus.Booked or CrmStatus.Completed;
        // A new enquiry from someone already further along doesn't send them back to "New lead".
        var keepFurther = status == CrmStatus.NewLead && customer.CrmStatus != CrmStatus.NewLead;
        if (!keepRepeat && !keepFurther)
        {
            SetCustomerStatus(db, customer, status, reason, adminUserId, now);
        }
    }

    public static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
