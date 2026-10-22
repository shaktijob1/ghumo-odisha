using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Bookings.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Contact;
using GhumoOdisha.Application.Crm;
using GhumoOdisha.Application.Crm.Dtos;
using GhumoOdisha.Application.Exceptions;
using GhumoOdisha.Domain.Entities;
using GhumoOdisha.Domain.Enums;
using GhumoOdisha.Infrastructure.Persistence;
using GhumoOdisha.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GhumoOdisha.Tests;

/// <summary>Admin → CRM services against the real database: customers, interests, follow-ups, messages, departures, reports.</summary>
public class CrmTests
{
    private static readonly IOptions<CrmOptions> Options = Microsoft.Extensions.Options.Options.Create(new CrmOptions());

    private sealed record Services(
        CrmCustomerService Customers,
        CrmTripInterestService Interests,
        CrmFollowUpService FollowUps,
        CrmTemplateService Templates,
        CrmMessageService Messages,
        CrmDepartureService Departures,
        CrmReportService Reports);

    private static Services Create(GhumoOdishaDbContext db)
    {
        var followUps = new CrmFollowUpService(db, Options);
        var departures = new CrmDepartureService(db, Options);
        return new Services(new CrmCustomerService(db), new CrmTripInterestService(db), followUps, new CrmTemplateService(db),
            new CrmMessageService(db), departures, new CrmReportService(db, followUps, departures));
    }

    private static async Task<int> AdminIdAsync(GhumoOdishaDbContext db)
    {
        var admin = await db.AdminUsers.FirstOrDefaultAsync(a => a.Role == "Admin");
        if (admin is not null)
        {
            return admin.AdminUserId;
        }

        admin = new AdminUser { Username = $"crm{Guid.NewGuid():N}"[..20], Email = $"{Guid.NewGuid():N}@test.local", PasswordHash = "x", Role = "Admin", CreatedAt = DateTime.UtcNow };
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
        return admin.AdminUserId;
    }

    /// <summary>A trip with stay and a pickup point, and one 16-seat departure (₹5,999, early ₹5,499 for 10 more days).</summary>
    private static async Task<(Trip Trip, TripDateSlot Slot)> SeedTripAsync(GhumoOdishaDbContext db)
    {
        var now = DateTime.UtcNow;
        var trip = new Trip { Title = $"CRM Koraput {Guid.NewGuid():N}"[..30], Description = "Automated test trip.", AmountPerPerson = 6000m, IncludesStay = true, Status = TripStatus.Active, CreatedAt = now, UpdatedAt = now };
        trip.PickupPoints.Add(new PickupPoint { Location = "Patia", Time = "5:30 AM", DisplayOrder = 1, CreatedAt = now });
        db.Trips.Add(trip);
        await db.SaveChangesAsync();

        var slot = new TripDateSlot
        {
            TripId = trip.TripId,
            StartDate = TripCalendar.Today().AddDays(40),
            EndDate = TripCalendar.Today().AddDays(42),
            TotalSeats = 16,
            AvailableSeats = 16,
            Status = TripDateSlotStatus.Active,
            Vehicle = "AC Traveller",
            MinimumBookedSeats = 10,
            PricePerPerson = 5999m,
            EarlyBirdPrice = 5499m,
            EarlyBirdEndsOn = TripCalendar.Today().AddDays(10),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.TripDateSlots.Add(slot);
        await db.SaveChangesAsync();
        return (trip, slot);
    }

    private static CreateCrmCustomerRequest NewCustomer(string phone, Trip? trip = null, TripDateSlot? slot = null, DateOnly? followUp = null) =>
        new("Rahul Test", phone, null, null, true, "Bhubaneswar", LeadSource.Instagram, CrmStatus.Interested, "Wants twin sharing", null,
            trip is null ? null : new CrmInterestInput(trip.TripId, slot?.TripDateSlotId, 2, CrmStatus.HotLead, "Asked about Patia pickup", 11000m),
            followUp, "Call about seats");

    // ---------- Pure helpers ----------

    [Theory]
    [InlineData("9876543210", "9876543210")]
    [InlineData("+91 98765-43210", "9876543210")]
    [InlineData("919876543210", "9876543210")]
    [InlineData("09876543210", "9876543210")]
    [InlineData("5876543210", null)]
    [InlineData("98765", null)]
    [InlineData("", null)]
    public void Phone_IsNormalizedToTenDigitIndianMobile(string input, string? expected) =>
        Assert.Equal(expected, CrmPhone.Normalize(input));

    [Fact]
    public void WhatsAppLink_EncodesTheMessageSafely()
    {
        var url = CrmPhone.AppUrl("919876543210", "Hi Rahul 👋\n₹5,499 & more?");
        Assert.StartsWith("https://wa.me/919876543210?text=", url);
        Assert.DoesNotContain(" ", url);
        Assert.DoesNotContain("\n", url);
        Assert.Contains("%26", url); // the & is encoded, not a new query parameter
        Assert.Equal("Hi Rahul 👋\n₹5,499 & more?", Uri.UnescapeDataString(url[(url.IndexOf("text=", StringComparison.Ordinal) + 5)..]));
        Assert.StartsWith("https://web.whatsapp.com/send?phone=919876543210&text=", CrmPhone.WebUrl("919876543210", "Hi"));
    }

    [Fact]
    public void Renderer_FillsKnownPlaceholders_ReportsMissingOnes_AndLeavesUnknownText()
    {
        var values = new CrmMessageValues("Rahul", "Koraput + Araku", new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 16), 5499m, 198m, 6, 16, 2, null, "AC Traveller", "AC Stay", null, null);
        var (message, missing) = CrmMessageRenderer.Render("Hi {name} 👋 {trip} 📅 {date} 💰 ₹{price}/person, {travellers} pax, pickup {pickup}. {Name}", values);

        Assert.Equal("Hi Rahul 👋 Koraput + Araku 📅 14-Nov-2026 💰 ₹5,499/person, 2 pax, pickup . Rahul", message);
        Assert.Equal(["pickup"], missing);
        Assert.Equal(["nmae"], CrmMessageRenderer.UnknownPlaceholders("Hi {nmae} {trip}"));
        Assert.Equal("1,23,456.50", CrmMessageRenderer.Money(123456.5m));

        // A line left with only an emoji by a missing value is dropped; lines with words stay.
        var (lines, missingStay) = CrmMessageRenderer.Render("Hi {name}\n🚐 {vehicle}\n🏨 {stay}\n🍳 Breakfast", values with { Stay = null });
        Assert.Equal("Hi Rahul\n🚐 AC Traveller\n🍳 Breakfast", lines);
        Assert.Equal(["stay"], missingStay);
    }

    [Fact]
    public void DepartureBadge_SaysConfirmedOnlyOnceTheMinimumIsBooked()
    {
        var options = new CrmOptions { FillingFastPercent = 50, AlmostFullSeatsLeft = 3 };
        var today = new DateOnly(2026, 10, 6);
        TripDateSlot Slot(int available, int? minimum = 10, TripDateSlotStatus status = TripDateSlotStatus.Active, int endInDays = 40) => new()
        {
            TotalSeats = 16, AvailableSeats = available, MinimumBookedSeats = minimum, Status = status,
            StartDate = today.AddDays(endInDays - 2), EndDate = today.AddDays(endInDays)
        };

        Assert.Equal(CrmDepartureStatus.OpenForBooking, CrmDepartureRules.StatusOf(Slot(16), today, options));
        Assert.Equal(CrmDepartureStatus.FillingFast, CrmDepartureRules.StatusOf(Slot(8), today, options));      // 8 booked, minimum 10 not reached
        Assert.Equal(CrmDepartureStatus.Confirmed, CrmDepartureRules.StatusOf(Slot(6), today, options));        // 10 booked
        Assert.Equal(CrmDepartureStatus.FillingFast, CrmDepartureRules.StatusOf(Slot(6, minimum: null), today, options)); // no minimum set: never "confirmed"
        Assert.Equal(CrmDepartureStatus.AlmostFull, CrmDepartureRules.StatusOf(Slot(2), today, options));
        Assert.Equal(CrmDepartureStatus.SoldOut, CrmDepartureRules.StatusOf(Slot(0), today, options));
        Assert.Equal(CrmDepartureStatus.Draft, CrmDepartureRules.StatusOf(Slot(16, status: TripDateSlotStatus.Inactive), today, options));
        Assert.Equal(CrmDepartureStatus.Completed, CrmDepartureRules.StatusOf(Slot(0, endInDays: -1), today, options));
    }

    // ---------- Customers ----------

    [Fact]
    public async Task AddCustomer_WithTripAndFollowUp_ThenDuplicateMobileIsRefused_AndFound()
    {
        await using var db = TestDb.CreateContext();
        var adminId = await AdminIdAsync(db);
        var (trip, slot) = await SeedTripAsync(db);
        var s = Create(db);
        var phone = TestDb.RandomPhoneNumber();

        var customerId = await s.Customers.CreateCustomerAsync(NewCustomer("+91 " + phone, trip, slot, TripCalendar.Today()), adminId);

        var detail = await s.Customers.GetCustomerAsync(customerId);
        Assert.Equal(phone, detail.PhoneNumber);
        Assert.Equal(LeadSource.Instagram, detail.LeadSource);
        Assert.False(detail.IsRegistered);
        var interest = Assert.Single(detail.Interests);
        Assert.Equal(CrmStatus.HotLead, interest.Status);
        Assert.Equal(slot.StartDate, interest.StartDate);
        Assert.Equal(TripCalendar.Today(), interest.NextFollowUpDate);
        Assert.Equal(TripCalendar.Today(), detail.NextFollowUpDate);
        // Both stages chosen in the add form are kept as entered.
        Assert.Equal(CrmStatus.Interested, detail.Status);
        Assert.NotEmpty(detail.StatusHistory);

        var duplicate = await Assert.ThrowsAsync<ConflictException>(() => s.Customers.CreateCustomerAsync(NewCustomer("0" + phone), adminId));
        Assert.Contains("already exists", duplicate.Message);
        var check = await s.Customers.CheckPhoneAsync(phone);
        Assert.True(check.Exists);
        Assert.Equal(customerId, check.CustomerId);
        Assert.Equal(1, check.InterestCount);
    }

    [Fact]
    public async Task AlternateMobile_CountsAsTakenToo()
    {
        await using var db = TestDb.CreateContext();
        var adminId = await AdminIdAsync(db);
        var s = Create(db);
        var main = TestDb.RandomPhoneNumber();
        var alternate = TestDb.RandomPhoneNumber();

        await s.Customers.CreateCustomerAsync(NewCustomer(main) with { AlternatePhoneNumber = alternate }, adminId);

        await Assert.ThrowsAsync<ConflictException>(() => s.Customers.CreateCustomerAsync(NewCustomer(alternate), adminId));
        Assert.True((await s.Customers.CheckPhoneAsync(alternate)).MatchedAlternate);
    }

    [Fact]
    public async Task Search_FindsByNameMobileTripAndBookingId()
    {
        await using var db = TestDb.CreateContext();
        var adminId = await AdminIdAsync(db);
        var (trip, slot) = await SeedTripAsync(db);
        var s = Create(db);
        var phone = TestDb.RandomPhoneNumber();
        var name = $"Searchable {Guid.NewGuid():N}"[..24];
        var customerId = await s.Customers.CreateCustomerAsync(NewCustomer(phone, trip, slot) with { Name = name }, adminId);

        // A real booking for them (through the normal admin booking path).
        var bookings = new BookingService(db, new FakeRazorpayService(), new FakeWhatsAppService(),
            Microsoft.Extensions.Options.Options.Create(new OrganizerContactOptions { WhatsAppNumber = "919000000000" }), NullLogger<BookingService>.Instance);
        var bookingId = await bookings.CreateManualBookingAsync(new CreateManualBookingRequest(
            customerId, null, null, null, trip.TripId, slot.TripDateSlotId, 1, 500m, PaymentMethod.Upi, null, BookingSource.Phone, null));
        var reference = await db.Bookings.Where(b => b.BookingId == bookingId).Select(b => "GO-" + b.BookingNumber).SingleAsync();

        foreach (var search in new[] { name, phone[^6..], trip.Title, reference })
        {
            var result = await s.Customers.GetCustomersAsync(new CrmCustomerFilter(Search: search, PageSize: 100));
            Assert.Contains(result.Items, c => c.CustomerId == customerId);
        }

        var listed = (await s.Customers.GetCustomersAsync(new CrmCustomerFilter(Search: name, ActiveOnly: true))).Items.Single();
        Assert.Equal(1, listed.TotalBookings);
        Assert.Equal(500m, listed.TotalSpent);
        Assert.Equal(1, listed.InterestCount);
    }

    // ---------- Trip interests ----------

    [Fact]
    public async Task InterestStages_KeepHistory_AndBookedNeedsARealBooking()
    {
        await using var db = TestDb.CreateContext();
        var adminId = await AdminIdAsync(db);
        var (trip, slot) = await SeedTripAsync(db);
        var s = Create(db);
        var customerId = await s.Customers.CreateCustomerAsync(NewCustomer(TestDb.RandomPhoneNumber()), adminId);

        var first = await s.Interests.CreateInterestAsync(new CreateTripInterestRequest(customerId, trip.TripId, slot.TripDateSlotId, 2, CrmStatus.Interested, null, null, TripCalendar.Today().AddDays(3)), adminId);
        // A second, brand-new enquiry doesn't send the customer back to "New lead".
        var second = await s.Interests.CreateInterestAsync(new CreateTripInterestRequest(customerId, trip.TripId, null, 4, CrmStatus.NewLead, "Maybe December", null, null), adminId);
        Assert.Equal(CrmStatus.Interested, (await s.Customers.GetCustomerAsync(customerId)).Status);

        await s.Interests.ChangeStatusAsync(first, new ChangeCrmStatusRequest(CrmStatus.BookingPending, "Will pay tomorrow"), adminId);
        await Assert.ThrowsAsync<ConflictException>(() => s.Interests.ChangeStatusAsync(first, new ChangeCrmStatusRequest(CrmStatus.Booked, null), adminId));
        await s.Interests.ChangeStatusAsync(second, new ChangeCrmStatusRequest(CrmStatus.NotInterested, "Dates don't suit"), adminId);

        var detail = await s.Customers.GetCustomerAsync(customerId);
        Assert.Equal(2, detail.Interests.Count);
        Assert.Equal("Dates don't suit", detail.Interests.Single(i => i.CustomerTripInterestId == second).LostReason);
        Assert.Contains(detail.StatusHistory, h => h.CustomerTripInterestId == first && h.FromStatus == CrmStatus.Interested && h.ToStatus == CrmStatus.BookingPending);

        var board = await s.Interests.GetPipelineAsync(trip.TripId, null, null, 30);
        Assert.Equal(CrmTripInterestService.PipelineStages, board.Columns.Select(c => c.Status));
        Assert.Contains(board.Columns.Single(c => c.Status == CrmStatus.BookingPending).Items, i => i.CustomerTripInterestId == first);

        var departure = await s.Departures.GetDepartureAsync(slot.TripDateSlotId);
        Assert.Equal(1, departure.Departure.BookingPending);
        Assert.Equal(1, departure.Departure.Leads);
    }

    // ---------- Follow-ups ----------

    [Fact]
    public async Task FollowUps_AreSplitIntoTodayOverdueUpcoming_AndCompletingSchedulesTheNext()
    {
        await using var db = TestDb.CreateContext();
        var adminId = await AdminIdAsync(db);
        var s = Create(db);
        var customerId = await s.Customers.CreateCustomerAsync(NewCustomer(TestDb.RandomPhoneNumber()), adminId);
        var today = TripCalendar.Today();

        var dueToday = await s.FollowUps.CreateFollowUpAsync(new CreateFollowUpRequest(customerId, null, today, "Call", null), adminId);
        var overdue = await s.FollowUps.CreateFollowUpAsync(new CreateFollowUpRequest(customerId, null, today.AddDays(-2), "Was due", null), adminId);
        var upcoming = await s.FollowUps.CreateFollowUpAsync(new CreateFollowUpRequest(customerId, null, today.AddDays(3), "Later", null), adminId);

        async Task<IReadOnlyList<CrmFollowUpDto>> Bucket(CrmFollowUpBucket bucket) =>
            (await s.FollowUps.GetFollowUpsAsync(new CrmFollowUpFilter(bucket, CustomerId: customerId))).Items;

        Assert.Equal([dueToday], (await Bucket(CrmFollowUpBucket.Today)).Select(f => f.CrmFollowUpId));
        var late = Assert.Single(await Bucket(CrmFollowUpBucket.Overdue));
        Assert.Equal(overdue, late.CrmFollowUpId);
        Assert.Equal(2, late.DaysOverdue);
        Assert.Equal([upcoming], (await Bucket(CrmFollowUpBucket.Upcoming)).Select(f => f.CrmFollowUpId));

        await s.FollowUps.CompleteAsync(overdue, new CompleteFollowUpRequest("Spoke — wants 3 seats", today.AddDays(1), "Send itinerary", CrmStatus.HotLead), adminId);
        Assert.Empty(await Bucket(CrmFollowUpBucket.Overdue));
        Assert.Equal(2, (await Bucket(CrmFollowUpBucket.Upcoming)).Count);
        var customer = await s.Customers.GetCustomerAsync(customerId);
        Assert.Equal(CrmStatus.HotLead, customer.Status);
        Assert.NotNull(customer.LastContactAt);

        await s.FollowUps.RescheduleAsync(dueToday, new RescheduleFollowUpRequest(today.AddDays(5), null));
        Assert.Empty(await Bucket(CrmFollowUpBucket.Today));
        await Assert.ThrowsAsync<ConflictException>(() => s.FollowUps.CompleteAsync(overdue, new CompleteFollowUpRequest(null, null, null, null), adminId));

        var summary = await s.FollowUps.GetSummaryAsync(null);
        Assert.True(summary.Upcoming >= 3);
    }

    // ---------- Templates and WhatsApp ----------

    [Fact]
    public async Task Template_FillsTheCustomersTrip_OpensWhatsApp_AndIsMarkedSentOnlyWhenTheAdminSaysSo()
    {
        await using var db = TestDb.CreateContext();
        var adminId = await AdminIdAsync(db);
        var (trip, slot) = await SeedTripAsync(db);
        var s = Create(db);
        var phone = TestDb.RandomPhoneNumber();
        var customerId = await s.Customers.CreateCustomerAsync(NewCustomer(phone, trip, slot), adminId);

        await Assert.ThrowsAsync<ValidationAppException>(() => s.Templates.CreateTemplateAsync(new SaveCrmTemplateRequest($"Bad {Guid.NewGuid():N}", CrmTemplateCategory.Custom, "Hi {nmae}", true)));
        var template = await s.Templates.CreateTemplateAsync(new SaveCrmTemplateRequest(
            $"Follow-up {Guid.NewGuid():N}"[..30], CrmTemplateCategory.FollowUp,
            "Hi {name} 👋\nYou had enquired about our {trip}.\n📅 {date}\n💰 ₹{price}/person\n🚐 {vehicle}\n🏨 {stay}\nReserve for ₹{booking_amount} ({travellers} seats). Pickup: {pickup}. {available_seats}/{total_seats} seats left.", true));

        var preview = await s.Messages.RenderAsync(new RenderCrmMessageRequest(customerId, null, template.CrmWhatsAppTemplateId, null));
        Assert.Contains("Hi Rahul Test 👋", preview.Message);
        Assert.Contains("📅 " + CrmMessageRenderer.FormatDate(slot.StartDate), preview.Message);
        Assert.Contains("₹5,499/person", preview.Message); // early-booking price, from the departure
        Assert.Contains("🚐 AC Traveller", preview.Message);
        Assert.Contains("🏨 AC Stay", preview.Message);
        Assert.Contains("Reserve for ₹198 (2 seats)", preview.Message);
        Assert.Contains("Pickup: Patia.", preview.Message);
        Assert.Contains("16/16 seats left", preview.Message);
        Assert.Empty(preview.MissingValues);
        Assert.Equal("91" + phone, preview.WhatsAppPhone);

        var link = await s.Messages.OpenWhatsAppAsync(new OpenWhatsAppRequest(customerId, null, template.CrmWhatsAppTemplateId, preview.Message + "\nEdited by admin"), adminId);
        Assert.StartsWith($"https://wa.me/91{phone}?text=", link.AppUrl);
        Assert.StartsWith($"https://web.whatsapp.com/send?phone=91{phone}&text=", link.WebUrl);

        // Opening WhatsApp is only "opened" — not sent, delivered or read.
        var log = await db.CrmCommunications.AsNoTracking().SingleAsync(c => c.CrmCommunicationId == link.CrmCommunicationId);
        Assert.Equal(CrmMessageStatus.WhatsAppOpened, log.Status);
        Assert.Equal(template.Name, log.TemplateName);
        Assert.EndsWith("Edited by admin", log.MessagePreview);
        Assert.Null((await s.Customers.GetCustomerAsync(customerId)).LastContactAt);

        await s.Messages.MarkSentAsync(link.CrmCommunicationId, adminId);
        var history = await s.Messages.GetHistoryAsync(new CrmCommunicationFilter(CustomerId: customerId));
        var sent = Assert.Single(history.Items);
        Assert.Equal(CrmMessageStatus.SentManually, sent.Status);
        Assert.NotNull(sent.SentConfirmedAt);
        Assert.NotNull((await s.Customers.GetCustomerAsync(customerId)).LastContactAt);
        await Assert.ThrowsAsync<ConflictException>(() => s.Messages.MarkNotSentAsync(link.CrmCommunicationId));

        // The log keeps the template's name after the template is deleted.
        await s.Templates.DeleteTemplateAsync(template.CrmWhatsAppTemplateId);
        Assert.Equal(template.Name, (await s.Messages.GetHistoryAsync(new CrmCommunicationFilter(CustomerId: customerId))).Items.Single().TemplateName);
    }

    [Fact]
    public async Task WhatsApp_IsRefused_ForACustomerMarkedNotOnWhatsApp_AndCallsCanBeLogged()
    {
        await using var db = TestDb.CreateContext();
        var adminId = await AdminIdAsync(db);
        var s = Create(db);
        var customerId = await s.Customers.CreateCustomerAsync(NewCustomer(TestDb.RandomPhoneNumber()) with { WhatsAppAvailable = false }, adminId);

        await Assert.ThrowsAsync<ConflictException>(() => s.Messages.OpenWhatsAppAsync(new OpenWhatsAppRequest(customerId, null, null, "Hi"), adminId));

        await s.Messages.LogAsync(new LogCommunicationRequest(customerId, null, CrmChannel.Phone, "Called — no answer", null), adminId);
        var logged = Assert.Single((await s.Messages.GetHistoryAsync(new CrmCommunicationFilter(CustomerId: customerId))).Items);
        Assert.Equal(CrmMessageStatus.Logged, logged.Status);
        Assert.Equal(CrmChannel.Phone, logged.Channel);

        // The history page finds it by the customer's mobile, and filters by channel.
        var phone = (await s.Customers.GetCustomerAsync(customerId)).PhoneNumber!;
        Assert.Contains((await s.Messages.GetHistoryAsync(new CrmCommunicationFilter(Search: "+91 " + phone))).Items, c => c.CustomerId == customerId);
        Assert.DoesNotContain((await s.Messages.GetHistoryAsync(new CrmCommunicationFilter(Search: phone, Channel: CrmChannel.WhatsApp))).Items, c => c.CustomerId == customerId);
    }

    [Fact]
    public async Task Templates_CanBeDuplicatedAndDeactivated()
    {
        await using var db = TestDb.CreateContext();
        var s = Create(db);
        var name = $"Limited {Guid.NewGuid():N}"[..30];
        var template = await s.Templates.CreateTemplateAsync(new SaveCrmTemplateRequest(name, CrmTemplateCategory.LimitedSeats, "Only {available_seats} seats left!", true));

        var copy = await s.Templates.DuplicateTemplateAsync(template.CrmWhatsAppTemplateId);
        Assert.Equal($"{name} (copy)", copy.Name);
        Assert.False(copy.IsActive);
        Assert.Equal($"{name} (copy 2)", (await s.Templates.DuplicateTemplateAsync(template.CrmWhatsAppTemplateId)).Name);
        await Assert.ThrowsAsync<ConflictException>(() => s.Templates.CreateTemplateAsync(new SaveCrmTemplateRequest(name, CrmTemplateCategory.Custom, "x", true)));

        await s.Templates.SetActiveAsync(template.CrmWhatsAppTemplateId, false);
        Assert.DoesNotContain(await s.Templates.GetTemplatesAsync(true, null), t => t.Name.StartsWith(name));
    }

    // ---------- Departures, dashboard, reports ----------

    [Fact]
    public async Task CreateDeparture_MakesARealBookableDate_WithValidatedEarlyBooking()
    {
        await using var db = TestDb.CreateContext();
        var (trip, _) = await SeedTripAsync(db);
        var s = Create(db);
        var start = TripCalendar.Today().AddDays(60);

        await Assert.ThrowsAsync<ValidationAppException>(() => s.Departures.CreateDepartureAsync(new CreateCrmDepartureRequest(
            trip.TripId, start, start.AddDays(2), 16, true, "AC Traveller", 10, 5999m, 6499m, start.AddDays(-20))));   // early price above regular
        await Assert.ThrowsAsync<ValidationAppException>(() => s.Departures.CreateDepartureAsync(new CreateCrmDepartureRequest(
            trip.TripId, start, start.AddDays(2), 16, true, null, 20, null, null, null)));                              // minimum above seats

        var slotId = await s.Departures.CreateDepartureAsync(new CreateCrmDepartureRequest(
            trip.TripId, start, start.AddDays(2), 16, true, "AC Traveller", 10, 5999m, 5499m, TripCalendar.Today().AddDays(9)));

        var slot = await db.TripDateSlots.AsNoTracking().SingleAsync(x => x.TripDateSlotId == slotId);
        Assert.Equal(TripDateSlotStatus.Active, slot.Status);
        Assert.Equal(16, slot.AvailableSeats);

        var departure = (await s.Departures.GetDepartureAsync(slotId)).Departure;
        Assert.Equal(5999m, departure.RegularPrice);
        Assert.Equal(5499m, departure.CurrentPrice);
        Assert.True(departure.IsEarlyBird);
        Assert.Equal(CrmDepartureStatus.OpenForBooking, departure.CampaignStatus);
        Assert.False(departure.MinimumReached);

        await Assert.ThrowsAsync<ConflictException>(() => s.Departures.CreateDepartureAsync(new CreateCrmDepartureRequest(
            trip.TripId, start, start.AddDays(2), 10, true, null, null, null, null, null)));                            // same trip, same date

        await s.Departures.UpdateDetailsAsync(slotId, new UpdateCrmDepartureDetailsRequest("Innova", 8, 6200m, null, null));
        var updated = (await s.Departures.GetDepartureAsync(slotId)).Departure;
        Assert.Equal(6200m, updated.CurrentPrice);
        Assert.False(updated.IsEarlyBird);
        Assert.Equal("Innova", updated.Vehicle);
    }

    [Fact]
    public async Task DashboardAndReports_CountLeadsAndRealBookings_ForAnyMonth()
    {
        await using var db = TestDb.CreateContext();
        var adminId = await AdminIdAsync(db);
        var (trip, slot) = await SeedTripAsync(db);
        var s = Create(db);
        var customerId = await s.Customers.CreateCustomerAsync(NewCustomer(TestDb.RandomPhoneNumber(), trip, slot), adminId);
        await s.Interests.CreateInterestAsync(new CreateTripInterestRequest(customerId, trip.TripId, null, 1, CrmStatus.Interested, null, null, null), adminId);

        var today = TripCalendar.Today();
        var dashboard = await s.Reports.GetDashboardAsync(new CrmDashboardFilter(today.Year, today.Month, trip.TripId));
        Assert.Equal(2, dashboard.TotalLeads);
        Assert.Equal(1, dashboard.HotLeads);
        Assert.Equal(1, dashboard.Interested);
        Assert.Equal(0m, dashboard.ConversionRate);
        Assert.Equal(1, dashboard.TotalCustomers);

        // Another month (any year) simply has no leads for this trip.
        var empty = await s.Reports.GetDashboardAsync(new CrmDashboardFilter(2031, 2, trip.TripId));
        Assert.Equal(0, empty.TotalLeads);

        var monthly = await s.Reports.GetMonthlyReportAsync(today.Year, today.Month);
        Assert.True(monthly.TotalLeads >= 2);
        Assert.Contains(monthly.TopSources, r => r.Source == LeadSource.Instagram && r.Leads >= 2);

        var sources = await s.Reports.GetSourceReportAsync(today.AddDays(-1), today);
        Assert.Equal(Enum.GetValues<LeadSource>().Length, sources.Rows.Count);
        Assert.True(sources.Rows.Single(r => r.Source == LeadSource.Instagram).Leads >= 2);
    }

    // ---------- Bookings flow into the CRM ----------

    private static BookingService Bookings(GhumoOdishaDbContext db) =>
        new(db, new FakeRazorpayService(), new FakeWhatsAppService(),
            Microsoft.Extensions.Options.Options.Create(new OrganizerContactOptions { WhatsAppNumber = "919000000000" }), NullLogger<BookingService>.Instance);

    private static async Task<int> WebsiteBookingAsync(BookingService bookings, int customerId, Trip trip, TripDateSlot slot, int seats)
    {
        var request = await bookings.RequestBookingAsync(customerId, new CreateBookingRequest(trip.TripId, slot.TripDateSlotId, seats, null, AgreedToTerms: true));
        await bookings.ConfirmBookingAsync(request.Booking.BookingId, new ConfirmBookingRequest(99m * seats, 0m, "pay_test", PaymentMethod.Razorpay, null));
        return request.Booking.BookingId;
    }

    [Fact]
    public async Task WebsiteBooking_LinksTheOpenEnquiry_MarksItBooked_AndASecondTripMakesARepeatCustomer()
    {
        await using var db = TestDb.CreateContext();
        var adminId = await AdminIdAsync(db);
        var (trip, slot) = await SeedTripAsync(db);
        var s = Create(db);
        var customerId = await s.Customers.CreateCustomerAsync(NewCustomer(TestDb.RandomPhoneNumber(), trip, slot), adminId);
        var enquiryId = (await s.Customers.GetCustomerAsync(customerId)).Interests.Single().CustomerTripInterestId;

        var firstBooking = await WebsiteBookingAsync(Bookings(db), customerId, trip, slot, 2);

        db.ChangeTracker.Clear();
        var afterFirst = await s.Customers.GetCustomerAsync(customerId);
        var linked = Assert.Single(afterFirst.Interests);
        Assert.Equal(enquiryId, linked.CustomerTripInterestId);
        Assert.Equal(firstBooking, linked.BookingId);
        Assert.Equal(CrmStatus.Booked, linked.Status);
        Assert.Equal(CrmStatus.Booked, afterFirst.Status);
        Assert.Contains(afterFirst.StatusHistory, h => h.ToStatus == CrmStatus.Booked && h.AdminName == null && h.Reason!.Contains("confirmed"));

        // A second trip, booked straight from the website without an enquiry: a new interest, and a repeat customer.
        var (trip2, slot2) = await SeedTripAsync(db);
        await WebsiteBookingAsync(Bookings(db), customerId, trip2, slot2, 1);
        db.ChangeTracker.Clear();
        var afterSecond = await s.Customers.GetCustomerAsync(customerId);
        Assert.Equal(2, afterSecond.Interests.Count);
        Assert.Equal(CrmStatus.Booked, afterSecond.Interests.Single(i => i.TripId == trip2.TripId).Status);
        Assert.Equal(CrmStatus.RepeatCustomer, afterSecond.Status);

        // The departure page counts it as booked.
        Assert.Equal(1, (await s.Departures.GetDepartureAsync(slot.TripDateSlotId)).Departure.Booked);

        // Cancelling the first booking marks that trip Cancelled, with the reason as the lost reason.
        await Bookings(db).CancelBookingAsync(firstBooking, new CancelBookingRequest(null, false, "Family emergency"));
        db.ChangeTracker.Clear();
        var cancelled = (await s.Customers.GetCustomerAsync(customerId)).Interests.Single(i => i.BookingId == firstBooking);
        Assert.Equal(CrmStatus.Cancelled, cancelled.Status);
        Assert.Equal("Family emergency", cancelled.LostReason);
    }

    [Fact]
    public async Task ManualPhoneBooking_ForANewCustomer_RecordsPhoneAsTheSource_AndShowsInTheCrm()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot) = await SeedTripAsync(db);
        var phone = TestDb.RandomPhoneNumber();

        var bookingId = await Bookings(db).CreateManualBookingAsync(new CreateManualBookingRequest(
            null, "Walk-in Wanderer", phone, null, trip.TripId, slot.TripDateSlotId, 2, 1000m, PaymentMethod.Cash, null, BookingSource.Phone, null));

        db.ChangeTracker.Clear();
        var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.PhoneNumber == phone);
        Assert.Equal(LeadSource.Phone, customer.LeadSource);
        var interest = await db.CustomerTripInterests.AsNoTracking().SingleAsync(i => i.BookingId == bookingId);
        Assert.Equal(CrmStatus.Booked, interest.Status);
        Assert.Equal(slot.TripDateSlotId, interest.TripDateSlotId);
        Assert.Equal(2, interest.Travellers);
    }

    [Fact]
    public async Task FinishedTrip_IsMarkedCompleted_AndSyncingAgainChangesNothing()
    {
        await using var db = TestDb.CreateContext();
        var (trip, slot) = await SeedTripAsync(db);
        var s = Create(db);
        var customerId = await s.Customers.CreateCustomerAsync(NewCustomer(TestDb.RandomPhoneNumber()), await AdminIdAsync(db));
        var bookingId = await WebsiteBookingAsync(Bookings(db), customerId, trip, slot, 1);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Bookings SET BookingStatus = {(int)BookingStatus.Completed} WHERE BookingId = {bookingId}");
        db.ChangeTracker.Clear();

        await CrmBookingSync.SyncAsync(db, bookingId);
        await CrmBookingSync.SyncAsync(db, bookingId);
        db.ChangeTracker.Clear();

        var interest = await db.CustomerTripInterests.AsNoTracking().SingleAsync(i => i.BookingId == bookingId);
        Assert.Equal(CrmStatus.Completed, interest.Status);
        Assert.Equal(1, await db.CrmStatusChanges.CountAsync(h => h.CustomerTripInterestId == interest.CustomerTripInterestId && h.ToStatus == CrmStatus.Completed));
    }

    // ---------- CSV import ----------

    [Fact]
    public async Task Import_PreviewsEveryRow_NeverDuplicatesAMobile_AndAddsTripsToExistingCustomers()
    {
        await using var db = TestDb.CreateContext();
        var adminId = await AdminIdAsync(db);
        var (trip, slot) = await SeedTripAsync(db);
        var s = Create(db);
        var existingPhone = TestDb.RandomPhoneNumber();
        var existingId = await s.Customers.CreateCustomerAsync(NewCustomer(existingPhone), adminId);
        var newPhone = TestDb.RandomPhoneNumber();
        var departure = slot.StartDate.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var import = new CrmImportService(db, s.Customers, s.Interests);

        CrmImportRow Row(string? name, string? mobile, string? tripName = null, string? date = null, string? status = null, string? source = null) =>
            new(name, mobile, null, "Puri", tripName, date, status, source, "From the Instagram sheet", 2);
        var rows = new List<CrmImportRow>
        {
            Row("Asha Import", "+91 " + newPhone, trip.Title, departure, "Hot lead", "insta"),     // 1 new, with trip + departure
            Row("Asha Again", "0" + newPhone, trip.Title, null, "interested", "Instagram"),       // 2 same mobile as row 1
            Row("Existing One", existingPhone, trip.Title, departure, "Follow-up", "Referral"),   // 3 already a customer
            Row("Bad Mobile", "12345"),                                                            // 4 invalid
            Row("Booked Already", TestDb.RandomPhoneNumber(), trip.Title, null, "Booked"),         // 5 invalid: Booked follows bookings
            Row("No Such Trip", TestDb.RandomPhoneNumber(), "Trip that does not exist zz"),        // 6 invalid
        };

        var preview = await import.PreviewAsync(new CrmImportRequest(rows, AddInterestToExisting: true));
        Assert.Equal([CrmImportOutcome.New, CrmImportOutcome.SameAsEarlierRow, CrmImportOutcome.Existing, CrmImportOutcome.Invalid, CrmImportOutcome.Invalid, CrmImportOutcome.Invalid],
            preview.Rows.Select(r => r.Outcome));
        Assert.Equal(slot.TripDateSlotId, preview.Rows[0].TripDateSlotId);
        Assert.Equal(CrmStatus.HotLead, preview.Rows[0].Status);
        Assert.Equal(LeadSource.Instagram, preview.Rows[0].Source);
        Assert.Equal(existingId, preview.Rows[2].ExistingCustomerId);
        Assert.False(await db.Customers.AnyAsync(c => c.PhoneNumber == newPhone));   // the preview saves nothing

        var result = await import.ImportAsync(new CrmImportRequest(rows, AddInterestToExisting: true), adminId);
        Assert.Equal(1, result.CustomersCreated);
        Assert.Equal(3, result.InterestsAdded);
        Assert.Equal(3, result.Invalid);

        db.ChangeTracker.Clear();
        var created = await db.Customers.AsNoTracking().Include(c => c.TripInterests).SingleAsync(c => c.PhoneNumber == newPhone);
        Assert.Equal(LeadSource.Instagram, created.LeadSource);
        Assert.Equal(2, created.TripInterests.Count);
        Assert.Single(await db.CustomerTripInterests.AsNoTracking().Where(i => i.CustomerId == existingId).ToListAsync());
    }
}
