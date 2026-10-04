using System.Globalization;
using GhumoOdisha.Application.Bookings;
using GhumoOdisha.Application.Payments;

namespace GhumoOdisha.Application.Seo;

/// <summary>One question and its answer, shown on the page and repeated in FAQPage structured data.</summary>
public record FaqItem(string Question, string Answer);

/// <summary>A destination a trip covers, linking the trip page to that destination's page.</summary>
public record TripDestinationLink(string Name, string Slug);

/// <summary>
/// Text for the trip page's "Good to know" FAQ and the site-wide FAQ. Every answer is built from
/// the trip's own data (pickups, inclusions, dates) or from the booking rules the backend enforces
/// (booking amount, online cutoff, Terms &amp; Conditions §2/§3/§8) — nothing here is a promise
/// the business hasn't made. The API returns these to the Angular page and SeoPageRenderer writes
/// the same list into the first HTML response, so both always say the same thing.
/// </summary>
public static class TripPageContent
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    /// <summary>Where the business is based and its trips leave from (Company:Address). A trip page only
    /// says "from Bhubaneswar" when that trip's own itinerary or pickups name the city.</summary>
    public const string HomeCity = "Bhubaneswar";

    /// <summary>The city a trip leaves from, if its itinerary or pickup points name it ("Departure from Bhubaneswar").</summary>
    public static string? DetectDepartureCity(string departureCity, IEnumerable<string?> itineraryText)
    {
        if (string.IsNullOrWhiteSpace(departureCity))
        {
            return null;
        }

        return itineraryText.Any(text => text is not null && text.Contains(departureCity, StringComparison.OrdinalIgnoreCase))
            ? departureCity
            : null;
    }

    /// <summary>Same captions, same order as the trip page's "What's included" tiles.</summary>
    public static IReadOnlyList<string> IncludedItems(Trips.Dtos.TripInclusionsDto inc)
    {
        var items = new List<string>();
        if (inc.Breakfast) items.Add("Breakfast");
        if (inc.Lunch) items.Add("Lunch");
        if (inc.Dinner) items.Add("Dinner");
        if (inc.Stay) items.Add("AC Room");
        if (inc.AcVehicle) items.Add("AC Vehicle");
        if (inc.PushbackVehicle) items.Add("Pushback Vehicle");
        if (inc.Camping) items.Add("Camping");
        if (inc.Bonfire) items.Add("Bonfire");
        if (inc.MusicalNight) items.Add("Musical Night");
        if (inc.SwimmingPool) items.Add("Swimming Pool");
        if (inc.Coordinator) items.Add("Coordinator");
        return items;
    }

    /// <summary>"4 Days / 3 Nights" — same wording as the trip cards.</summary>
    public static string DurationLabel(DateOnly start, DateOnly end)
    {
        var days = end.DayNumber - start.DayNumber + 1;
        var nights = days - 1;
        return $"{days} {(days == 1 ? "Day" : "Days")} / {nights} {(nights == 1 ? "Night" : "Nights")}";
    }

    public static string Rupees(decimal amount) => "₹" + amount.ToString("#,##0.##", India);

    /// <summary>"Koraput Explore" from "KORAPUT EXPLORE"; "Puri – Konark – Chandrabhaga" from "Puri . Konark . Chandrabhaga".</summary>
    public static string DisplayName(string title)
    {
        var name = System.Text.RegularExpressions.Regex.Replace(title.Trim(), @"\s+[.•·|]\s+", " – ");
        name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ");
        var letters = name.Where(char.IsLetter).ToList();
        if (letters.Count > 0 && letters.All(char.IsUpper))
        {
            name = India.TextInfo.ToTitleCase(name.ToLower(India));
        }
        return name;
    }

    public static IReadOnlyList<FaqItem> BuildTripFaqs(
        string tripTitle,
        string? departureCity,
        IReadOnlyList<(string Location, string Time)> pickupPoints,
        IReadOnlyList<string> includedItems,
        string? durationLabel,
        bool hasUpcomingDates)
    {
        var name = DisplayName(tripTitle);
        var faqs = new List<FaqItem>();

        if (pickupPoints.Count > 0)
        {
            var from = departureCity is null ? "" : $" from {departureCity}";
            var stops = string.Join("; ", pickupPoints.Select(p => string.IsNullOrWhiteSpace(p.Time) ? p.Location : $"{p.Location} ({p.Time})"));
            faqs.Add(new FaqItem(
                $"Where does the {name} trip start{from}?",
                $"The group leaves{from} with pickups at: {stops}. You choose your pickup point while booking. Please reach it 10 minutes early, as the group cannot wait beyond a reasonable time."));
        }
        else if (departureCity is not null)
        {
            faqs.Add(new FaqItem($"Where does the {name} trip start?", $"The trip departs from {departureCity}."));
        }

        if (durationLabel is not null)
        {
            faqs.Add(new FaqItem(
                $"How long is the {name} trip?",
                $"The trip is {durationLabel}, from departure to return. The day-wise itinerary above lists every stop; timings may shift slightly with weather and traffic."));
        }

        if (includedItems.Count > 0)
        {
            faqs.Add(new FaqItem(
                "What is included in the trip price?",
                $"Included: {string.Join(", ", includedItems)}. Anything not listed — for example a meal that isn't marked as included — is at your own expense."));
        }

        faqs.Add(new FaqItem(
            "How do I book a seat?",
            $"Pick a departure date on this page, choose the number of travellers and your pickup point, and pay the booking amount of {Rupees(BookingPaymentService.PerSeatAdvanceAmount)} per seat online. " +
            $"Your seat is confirmed as soon as the payment goes through, and the balance is paid before departure as our team tells you. " +
            $"Online booking closes {SlotBookingRules.OnlineCutoffDays} days before departure — for a closer date, message us on WhatsApp."));

        faqs.Add(new FaqItem(
            "Can I cancel my booking?",
            $"Yes. Cancel up to 72 hours before the trip start date for a refund of the amount paid, except the {Rupees(BookingPaymentService.PerSeatAdvanceAmount)} per seat booking amount, which is non-refundable. " +
            "Cancellations within 72 hours of the start date, no-shows and missed pickups are not refunded. Approved refunds reach you within 7–10 working days."));

        faqs.Add(new FaqItem(
            "What happens if Ghumo Odisha cancels the trip?",
            "You are offered another date or a full refund of everything you paid, including the booking amount."));

        faqs.Add(new FaqItem(
            "What should I carry?",
            "Every traveller must carry a valid government photo ID (Aadhaar, Voter ID, Driving Licence or Passport) for the whole trip, plus any personal medication. Travellers under 18 must travel with a parent or guardian."));

        if (!hasUpcomingDates)
        {
            faqs.Add(new FaqItem(
                $"When is the next {name} departure?",
                "No dates are open right now. Message us on WhatsApp and we'll tell you as soon as new dates are added."));
        }

        return faqs;
    }

    /// <summary>Questions answered from the destination's own admin-entered facts (season, distance, duration, highlights).</summary>
    public static IReadOnlyList<FaqItem> BuildDestinationFaqs(string name, string? bestSeason, string? distance, string? idealDuration, string? knownFor)
    {
        var faqs = new List<FaqItem>();
        if (!string.IsNullOrWhiteSpace(bestSeason))
            faqs.Add(new FaqItem($"What is the best time to visit {name}?", $"The best season for a {name} trip is {bestSeason.Trim()}."));
        if (!string.IsNullOrWhiteSpace(distance))
            faqs.Add(new FaqItem($"How far is {name} from {HomeCity}?", $"{name} is about {distance.Trim().TrimStart('~').Trim()} from {HomeCity} by road."));
        if (!string.IsNullOrWhiteSpace(idealDuration))
            faqs.Add(new FaqItem($"How many days do I need for {name}?", $"Plan {CleanLabel(idealDuration)} for {name}."));
        if (!string.IsNullOrWhiteSpace(knownFor))
            faqs.Add(new FaqItem($"What is {name} known for?", $"{name} is known for {JoinAnd(SplitList(knownFor))}."));
        return faqs;
    }

    /// <summary>Admin-entered text like "deal duration: 2–3 days" → "2–3 days".</summary>
    public static string CleanLabel(string text) => System.Text.RegularExpressions.Regex.Replace(text.Trim(), @"^[^:]*:\s*", "");

    public static List<string> SplitList(string text) =>
        text.Split(['•', '|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public static string JoinAnd(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1],
    };

    // ---------- Home page ----------

    /// <summary>Second line of the home page's H1, under "Meet. Travel. Explore."</summary>
    public const string HomeHeadingSub = "Explore Odisha with Ghumo Odisha";

    public static readonly string HomeIntro =
        $"Ghumo Odisha runs fixed-departure group trips from {HomeCity} to hills, waterfalls, temples, lakes and beaches across Odisha and nearby. " +
        "Pick a trip, choose a date and book your seat online in minutes.";

    /// <summary>The home page's "How booking works" steps — the real booking flow.</summary>
    public static readonly IReadOnlyList<FaqItem> BookingSteps =
    [
        new("Pick a trip and date", "Browse upcoming departures and open a trip to see its day-wise itinerary, stay, transport and what's included."),
        new("Choose travellers and pickup", $"Select how many gents and ladies are travelling and the pickup point in {HomeCity} that suits you."),
        new("Pay the booking amount", $"Pay {Rupees(BookingPaymentService.PerSeatAdvanceAmount)} per seat online — your seat is confirmed instantly."),
        new("Travel with the group", "Pay the balance before departure as our team tells you, then meet the group and your coordinator at the pickup point."),
    ];

    /// <summary>General questions about booking with Ghumo Odisha, shown on the home page.</summary>
    public static IReadOnlyList<FaqItem> BuildSiteFaqs(string departureCity) =>
    [
        new("What is Ghumo Odisha?",
            $"Ghumo Odisha runs fixed-departure group trips across Odisha and nearby destinations, starting from {departureCity}. Each trip has a day-wise itinerary and fixed dates, with transport, stays, meals and a trip coordinator as listed under What's included on each trip's page."),
        new("How do I book a Ghumo Odisha trip?",
            $"Open a trip, pick a departure date, choose your travellers and pickup point, and pay the booking amount of {Rupees(BookingPaymentService.PerSeatAdvanceAmount)} per seat online. Your seat is confirmed instantly; the balance is paid before departure. You can also book by messaging us on WhatsApp."),
        new("Where do trips start from?",
            $"Trips start from {departureCity}, with several pickup points across the city listed on each trip's page."),
        new("Can I cancel a booking?",
            $"Cancel up to 72 hours before the trip start date for a refund of the amount paid, except the non-refundable {Rupees(BookingPaymentService.PerSeatAdvanceAmount)} per seat booking amount. Later cancellations, no-shows and missed pickups are not refunded."),
        new("Do trips have a coordinator?",
            "Trips that include a coordinator say so under What's included on the trip page. Bigger groups travel with both a lady coordinator and a gents coordinator."),
    ];
}
