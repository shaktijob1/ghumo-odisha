using System.Globalization;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Crm;

public interface ICrmImportService
{
    Task<CrmImportPreviewDto> PreviewAsync(CrmImportRequest request, CancellationToken cancellationToken = default);
    Task<CrmImportResultDto> ImportAsync(CrmImportRequest request, int adminUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// CSV import of leads (the browser reads the file; rows arrive here). Every row is checked first —
/// mobile, duplicates (against the database and earlier rows), trip, departure, stage and source — and
/// the preview shows exactly what will happen. Importing goes through the normal customer / trip
/// interest services, so the same duplicate protection and history apply as when adding by hand.
/// </summary>
public class CrmImportService(IGhumoOdishaDbContext db, ICrmCustomerService customers, ICrmTripInterestService interests) : ICrmImportService
{
    public const int MaxRows = 2000;

    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "d/M/yyyy", "d-M-yyyy", "dd-MMM-yyyy", "d-MMM-yyyy", "d MMM yyyy", "dd MMM yyyy", "d MMMM yyyy", "dd.MM.yyyy"];

    public async Task<CrmImportPreviewDto> PreviewAsync(CrmImportRequest request, CancellationToken cancellationToken = default)
    {
        var planned = await PlanAsync(request, cancellationToken);
        return new CrmImportPreviewDto(
            planned.Count,
            planned.Count(p => p.Result.Outcome == CrmImportOutcome.New),
            planned.Count(p => p.Result.Outcome is CrmImportOutcome.Existing or CrmImportOutcome.SameAsEarlierRow),
            planned.Count(p => p.Result.Outcome == CrmImportOutcome.Invalid),
            planned.Select(p => p.Result).ToList());
    }

    public async Task<CrmImportResultDto> ImportAsync(CrmImportRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        var planned = await PlanAsync(request, cancellationToken);
        int created = 0, added = 0, skipped = 0, invalid = 0;
        var errors = new List<string>();
        // Customers created by earlier rows of this file, by mobile.
        var createdByMobile = new Dictionary<string, int>();

        foreach (var row in planned)
        {
            var r = row.Result;
            try
            {
                switch (r.Outcome)
                {
                    case CrmImportOutcome.Invalid:
                        invalid++;
                        break;

                    case CrmImportOutcome.New:
                        var customerId = await customers.CreateCustomerAsync(new CreateCrmCustomerRequest(
                            row.Name!, row.Mobile!, null, row.Email, true, row.City, row.Source, row.Status, row.Notes, null,
                            r.TripId is { } tripId ? new CrmInterestInput(tripId, r.TripDateSlotId, row.Travellers, row.Status, null, null) : null,
                            null, null), adminUserId, cancellationToken);
                        createdByMobile[row.Mobile!] = customerId;
                        created++;
                        if (r.TripId is not null)
                        {
                            added++;
                        }

                        break;

                    case CrmImportOutcome.Existing or CrmImportOutcome.SameAsEarlierRow:
                        var targetId = r.ExistingCustomerId ?? createdByMobile.GetValueOrDefault(row.Mobile!);
                        var allowed = r.Outcome == CrmImportOutcome.SameAsEarlierRow || request.AddInterestToExisting;
                        if (targetId == 0 || r.TripId is null || !allowed)
                        {
                            skipped++;
                            break;
                        }

                        await interests.CreateInterestAsync(new CreateTripInterestRequest(
                            targetId, r.TripId.Value, r.TripDateSlotId, row.Travellers, row.Status, row.Notes, null, null), adminUserId, cancellationToken);
                        added++;
                        break;
                }
            }
            catch (AppException ex)
            {
                invalid++;
                errors.Add($"Row {r.Row}: {(ex is ValidationAppException v ? string.Join(" ", v.Errors) : ex.Message)}");
            }
        }

        return new CrmImportResultDto(created, added, skipped, invalid, errors);
    }

    private sealed record PlannedRow(CrmImportRowResult Result, string? Name, string? Mobile, string? Email, string? City, string? Notes,
        LeadSource Source, CrmStatus Status, int Travellers);

    private async Task<List<PlannedRow>> PlanAsync(CrmImportRequest request, CancellationToken cancellationToken)
    {
        if (request.Rows.Count == 0)
        {
            throw new ValidationAppException(["The file has no rows."]);
        }

        if (request.Rows.Count > MaxRows)
        {
            throw new ValidationAppException([$"Import up to {MaxRows} rows at a time."]);
        }

        var mobiles = request.Rows.Select(r => CrmPhone.Normalize(r.Mobile)).OfType<string>().Distinct().ToList();
        var existing = await db.Customers.AsNoTracking()
            .Where(c => (c.PhoneNumber != null && mobiles.Contains(c.PhoneNumber)) || (c.AlternatePhoneNumber != null && mobiles.Contains(c.AlternatePhoneNumber)))
            .Select(c => new { c.CustomerId, c.Name, c.PhoneNumber, c.AlternatePhoneNumber })
            .ToListAsync(cancellationToken);
        var trips = await db.Trips.AsNoTracking().Select(t => new { t.TripId, t.Title }).ToListAsync(cancellationToken);
        var slots = await db.TripDateSlots.AsNoTracking().Select(s => new { s.TripDateSlotId, s.TripId, s.StartDate }).ToListAsync(cancellationToken);

        var firstRowByMobile = new Dictionary<string, int>();
        var planned = new List<PlannedRow>();
        for (var index = 0; index < request.Rows.Count; index++)
        {
            var row = request.Rows[index];
            var rowNumber = index + 1;
            var errors = new List<string>();
            var warnings = new List<string>();

            var name = Clean(row.Name, 150);
            if (name is null)
            {
                errors.Add("Name is missing.");
            }

            var mobile = CrmPhone.Normalize(row.Mobile);
            if (mobile is null)
            {
                errors.Add(string.IsNullOrWhiteSpace(row.Mobile) ? "Mobile is missing." : $"\"{row.Mobile}\" isn't a 10-digit Indian mobile.");
            }

            var email = Clean(row.Email, 200);
            if (email is not null && !email.Contains('@'))
            {
                warnings.Add("Email looks wrong and was left out.");
                email = null;
            }

            var status = CrmStatus.NewLead;
            if (!string.IsNullOrWhiteSpace(row.Status))
            {
                var parsed = ParseStatus(row.Status);
                if (parsed is null)
                {
                    errors.Add($"Unknown status \"{row.Status}\".");
                }
                else if (parsed is CrmStatus.Booked or CrmStatus.Completed or CrmStatus.RepeatCustomer)
                {
                    errors.Add($"\"{row.Status}\" follows real bookings and can't be imported — use an enquiry stage.");
                }
                else
                {
                    status = parsed.Value;
                }
            }

            var source = LeadSource.Other;
            if (!string.IsNullOrWhiteSpace(row.Source))
            {
                var parsed = ParseSource(row.Source);
                if (parsed is null)
                {
                    warnings.Add($"Unknown source \"{row.Source}\" — saved as Other.");
                }
                else
                {
                    source = parsed.Value;
                }
            }

            int? tripId = null;
            string? tripTitle = null;
            int? slotId = null;
            if (!string.IsNullOrWhiteSpace(row.Trip))
            {
                var wanted = row.Trip.Trim();
                var matches = trips.Where(t => string.Equals(t.Title, wanted, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matches.Count == 0)
                {
                    matches = trips.Where(t => t.Title.Contains(wanted, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (matches.Count == 1)
                {
                    tripId = matches[0].TripId;
                    tripTitle = matches[0].Title;
                }
                else
                {
                    errors.Add(matches.Count == 0 ? $"No trip called \"{wanted}\"." : $"\"{wanted}\" matches {matches.Count} trips — use the full trip name.");
                }
            }

            if (!string.IsNullOrWhiteSpace(row.Departure))
            {
                if (!DateOnly.TryParseExact(row.Departure.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    errors.Add($"Can't read the departure date \"{row.Departure}\" (use e.g. 2026-11-14 or 14-Nov-2026).");
                }
                else if (tripId is null)
                {
                    warnings.Add("Departure ignored — no trip given.");
                }
                else
                {
                    slotId = slots.FirstOrDefault(s => s.TripId == tripId && s.StartDate == date)?.TripDateSlotId;
                    if (slotId is null)
                    {
                        warnings.Add($"{tripTitle} has no departure on {date:d MMM yyyy} — added without a date.");
                    }
                }
            }

            var travellers = row.Travellers is > 0 and <= 100 ? row.Travellers.Value : 1;

            CrmImportOutcome outcome;
            int? existingId = null;
            string? existingName = null;
            if (errors.Count > 0)
            {
                outcome = CrmImportOutcome.Invalid;
            }
            else if (existing.FirstOrDefault(c => c.PhoneNumber == mobile || c.AlternatePhoneNumber == mobile) is { } match)
            {
                outcome = CrmImportOutcome.Existing;
                existingId = match.CustomerId;
                existingName = match.Name;
                if (tripId is null)
                {
                    warnings.Add("Already a customer and no trip given — nothing to add.");
                }
            }
            else if (firstRowByMobile.TryGetValue(mobile!, out var firstRow))
            {
                outcome = CrmImportOutcome.SameAsEarlierRow;
                warnings.Add(tripId is null ? $"Same mobile as row {firstRow} and no trip — skipped." : $"Same mobile as row {firstRow} — adds this trip to that customer.");
            }
            else
            {
                outcome = CrmImportOutcome.New;
                firstRowByMobile[mobile!] = rowNumber;
            }

            planned.Add(new PlannedRow(
                new CrmImportRowResult(rowNumber, outcome, name, mobile, existingId, existingName, tripId, tripTitle, slotId, status, source, errors, warnings),
                name, mobile, email, Clean(row.City, 100), Clean(row.Notes, 2000), source, status, travellers));
        }

        return planned;
    }

    private static string? Clean(string? value, int max) => CrmHistory.Trim(value, max);

    /// <summary>"Hot lead", "hot", "HotLead", "booking-pending" … → the stage.</summary>
    private static CrmStatus? ParseStatus(string value)
    {
        var key = Key(value);
        if (key == "hot")
        {
            return CrmStatus.HotLead;
        }

        if (key == "new")
        {
            return CrmStatus.NewLead;
        }

        return Enum.GetValues<CrmStatus>().Cast<CrmStatus?>().FirstOrDefault(s => Key(s!.Value.ToString()) == key);
    }

    /// <summary>"Instagram", "insta", "walk-in", "phone call" … → the source.</summary>
    private static LeadSource? ParseSource(string value)
    {
        var key = Key(value);
        return key switch
        {
            "insta" or "ig" => LeadSource.Instagram,
            "fb" => LeadSource.Facebook,
            "wa" => LeadSource.WhatsApp,
            "phonecall" or "call" => LeadSource.Phone,
            "walkin" or "offline" => LeadSource.WalkIn,
            _ => Enum.GetValues<LeadSource>().Cast<LeadSource?>().FirstOrDefault(s => Key(s!.Value.ToString()) == key)
        };
    }

    private static string Key(string value) => new(value.Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray());
}
