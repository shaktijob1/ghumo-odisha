using System.Globalization;
using System.Text;
using GhumoOdisha.Application.Blog.Dtos;
using GhumoOdisha.Application.Destinations.Dtos;
using GhumoOdisha.Application.Homepage;
using GhumoOdisha.Application.Legal;
using GhumoOdisha.Application.Seo;
using GhumoOdisha.Application.Trips.Dtos;

namespace GhumoOdisha.Api.Seo;

/// <summary>
/// The server-rendered page bodies. Each one mirrors what the Angular page shows for the same URL
/// (same headings, text, prices, dates, links) — never extra or hidden content — in plain semantic
/// HTML that reads well before the app loads and for crawlers that don't run JavaScript.
/// </summary>
public partial class SeoPageRenderer
{
    // Small, self-contained styles for the server-rendered content (the app's own stylesheet takes
    // over with Angular). Same tokens as styles.css: white cards, hairline borders, one accent.
    private const string PrerenderCss =
        ".ssr{font-family:Poppins,system-ui,sans-serif;color:#0F1416;background:#F7F8F8;line-height:1.6}" +
        ".ssr a{color:#0F6F5C}.ssr-head{display:flex;justify-content:space-between;align-items:center;gap:16px;padding:14px 20px;background:#fff;border-bottom:1px solid #E7E9EA}" +
        ".ssr-logo{font-weight:700;font-size:17px;text-decoration:none;color:#0F1416!important}.ssr-logo em{font-style:normal;color:#0F6F5C}" +
        "" +
        ".ssr-main{max-width:1100px;margin:0 auto;padding:20px 16px 40px}.ssr-main h1{font-size:28px;line-height:1.25;margin:8px 0 10px}" +
        ".ssr-main h2{font-size:20px;margin:28px 0 10px}.ssr-main h3{font-size:16px;margin:16px 0 6px}" +
        ".ssr-sec{background:#fff;border:1px solid #E7E9EA;border-radius:14px;padding:16px 18px;margin-top:16px}" +
        ".ssr-crumb ol{display:flex;flex-wrap:wrap;gap:6px;list-style:none;padding:0;margin:0;font-size:13px;color:#6A7478}" +
        ".ssr-crumb li+li:before{content:'›';margin-right:6px}.ssr-hero{width:100%;aspect-ratio:16/7;object-fit:cover;border-radius:14px;background:#E7E9EA}" +
        ".ssr-facts{display:grid;grid-template-columns:repeat(auto-fit,minmax(160px,1fr));gap:10px;margin:14px 0;padding:0;list-style:none}" +
        ".ssr-facts li{background:#fff;border:1px solid #E7E9EA;border-radius:12px;padding:10px 12px;font-size:14px}.ssr-facts span{display:block;font-size:12px;color:#6A7478}" +
        ".ssr-list{padding-left:20px}.ssr-list li{margin:4px 0}.ssr-cards{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:12px;padding:0;list-style:none}" +
        ".ssr-cards li{background:#fff;border:1px solid #E7E9EA;border-radius:14px;padding:14px}.ssr-cards a{font-weight:600;text-decoration:none}" +
        ".ssr-muted{color:#6A7478;font-size:14px}.ssr-cta{display:inline-block;background:#0F6F5C;color:#fff!important;border-radius:12px;padding:10px 18px;text-decoration:none;font-weight:600}" +
        ".ssr-foot{border-top:1px solid #E7E9EA;background:#fff;padding:24px 20px;font-size:13px;color:#6A7478}.ssr-foot nav{display:flex;flex-wrap:wrap;gap:8px 16px;margin:6px 0 12px}" +
        ".ssr-faq dt{font-weight:600;margin-top:12px}.ssr-faq dd{margin:4px 0 0}.ssr-pre{white-space:pre-line}" +
        ".ssr-tags{display:flex;flex-wrap:wrap;gap:8px;list-style:none;padding:0;margin:0}.ssr-tags li{background:#fff;border:1px solid #E7E9EA;border-radius:999px;padding:4px 12px;font-size:13px}" +
        ".ssr-photos{display:grid;grid-template-columns:repeat(auto-fill,minmax(200px,1fr));gap:10px;padding:0;margin:0;list-style:none}.ssr-photos img{width:100%;aspect-ratio:4/3;object-fit:cover;border-radius:12px}";

    // Same small Odisha map as the Angular navbar (shared/components/odisha-mark.component.ts).
    private const string OdishaMark = "<svg viewBox=\"-3 -3 106 89\" width=\"30\" height=\"25\" aria-hidden=\"true\" style=\"vertical-align:middle;margin-right:7px\"><defs><linearGradient id=\"om-ssr\" gradientUnits=\"userSpaceOnUse\" x1=\"20\" y1=\"12\" x2=\"78\" y2=\"62\"><stop offset=\"0\" stop-color=\"#0F6F5C\"/><stop offset=\".38\" stop-color=\"#22A884\"/><stop offset=\".7\" stop-color=\"#F4B23E\"/><stop offset=\"1\" stop-color=\"#EE6A2A\"/></linearGradient></defs><path d=\"M76.9 0.0L91.5 7.9L92.5 11.6L95.9 10.1L96.5 12.7L99.7 14.1L100.0 16.3L93.1 18.8L89.8 23.0L91.6 30.0L88.6 31.3L92.9 31.8L87.5 36.0L88.4 38.7L81.7 45.2L66.5 50.8L55.4 60.5L53.8 59.0L52.5 61.5L49.7 61.5L48.0 65.6L41.0 65.1L36.7 59.1L35.2 61.6L34.0 60.5L33.9 62.7L31.5 61.9L33.2 64.2L27.3 68.5L27.4 72.7L23.2 71.6L20.4 75.2L17.9 69.9L14.5 79.5L10.5 78.2L3.7 82.5L0.0 82.6L2.3 74.7L5.9 73.2L9.2 69.5L8.2 67.9L11.4 66.8L14.0 63.3L13.1 54.5L10.4 53.0L11.1 48.1L7.6 45.9L7.9 43.7L9.1 42.5L13.9 44.3L15.6 47.3L17.3 46.0L19.8 46.6L19.7 48.3L21.6 47.2L21.7 44.4L16.5 43.2L15.8 28.8L17.6 29.9L20.5 24.2L28.4 25.0L30.9 20.2L33.1 20.7L32.0 18.1L36.1 12.0L35.2 8.7L36.7 5.7L42.9 2.9L42.8 0.1L47.6 3.4L60.3 0.9L61.1 4.2L59.1 7.9L63.0 9.3L65.2 6.6L70.4 8.4L72.4 7.3L71.7 9.5L74.1 9.9L76.4 3.9L75.0 0.9Z\" fill=\"url(#om-ssr)\" stroke=\"#fff\" stroke-width=\"2\" stroke-linejoin=\"round\"/><circle cx=\"72.7\" cy=\"39.2\" r=\"9\" fill=\"#fff\"/><circle cx=\"72.7\" cy=\"39.2\" r=\"5\" fill=\"#E2412F\"/></svg>";

    private sealed class HtmlWriter
    {
        private readonly StringBuilder _sb = new();
        public HtmlWriter Raw(string html) { _sb.Append(html); return this; }
        public HtmlWriter Text(string? text) { _sb.Append(Html.Encode(text ?? "")); return this; }
        public HtmlWriter Tag(string tag, string? text, string? cls = null)
        {
            _sb.Append('<').Append(tag);
            if (cls is not null) _sb.Append(" class=\"").Append(cls).Append('"');
            _sb.Append('>').Append(Html.Encode(text ?? "")).Append("</").Append(tag).Append('>');
            return this;
        }
        public HtmlWriter Link(string href, string text, string? cls = null)
        {
            _sb.Append("<a href=\"").Append(Html.Encode(href)).Append('"');
            if (cls is not null) _sb.Append(" class=\"").Append(cls).Append('"');
            _sb.Append('>').Append(Html.Encode(text)).Append("</a>");
            return this;
        }
        public override string ToString() => _sb.ToString();
    }

    // ---------- Shared frame ----------

    private string Page(Action<HtmlWriter> main, (string Name, string Path)[]? breadcrumbs = null)
    {
        var w = new HtmlWriter();
        w.Raw("<div class=\"ssr\"><header class=\"ssr-head\"><a href=\"/\" class=\"ssr-logo\">" + OdishaMark + "Ghumo <em>Odisha</em></a></header>");
        w.Raw("<main class=\"ssr-main\">");
        if (breadcrumbs is { Length: > 0 })
        {
            w.Raw("<nav class=\"ssr-crumb\" aria-label=\"Breadcrumb\"><ol><li>").Link("/", "Home").Raw("</li>");
            for (var i = 0; i < breadcrumbs.Length; i++)
            {
                var (name, path) = breadcrumbs[i];
                w.Raw("<li>");
                if (i == breadcrumbs.Length - 1) w.Raw("<span aria-current=\"page\">").Text(name).Raw("</span>");
                else w.Link(path, name);
                w.Raw("</li>");
            }
            w.Raw("</ol></nav>");
        }
        main(w);
        w.Raw("</main>");
        Footer(w);
        w.Raw("</div>");
        return w.ToString();
    }

    private void Footer(HtmlWriter w)
    {
        var contact = contactOptions.Value;
        var company = companyOptions.Value;
        w.Raw("<footer class=\"ssr-foot\">");
        w.Raw("<p>");
        if (!string.IsNullOrWhiteSpace(contact.Phone)) w.Text("Call ").Link("tel:" + contact.Phone, contact.Phone).Text(" · ");
        if (!string.IsNullOrWhiteSpace(contact.WhatsAppNumber)) w.Link("https://wa.me/" + contact.WhatsAppNumber, "WhatsApp").Text(" · ");
        if (!string.IsNullOrWhiteSpace(contact.Email)) w.Link("mailto:" + contact.Email, contact.Email);
        if (!string.IsNullOrWhiteSpace(contact.InstagramUrl)) w.Text(" · ").Link(contact.InstagramUrl!, "Instagram");
        w.Raw("</p>");
        if (company.DisplayAddress() is { } address)
        {
            w.Raw("<address>").Text($"Visit our office: {address}");
            if (company.DirectionsUrl() is { } map) w.Text(" · ").Link(map, "Get directions");
            w.Raw("</address>");
        }
        w.Raw("<p>").Text($"© {DateTime.UtcNow.Year} Ghumo Odisha. All rights reserved. · ").Link("/terms", "Terms & Conditions").Raw("</p></footer>");
    }

    private static void Faqs(HtmlWriter w, string heading, IReadOnlyList<FaqItem> faqs)
    {
        if (faqs.Count == 0) return;
        w.Raw("<section class=\"ssr-sec\">").Tag("h2", heading).Raw("<dl class=\"ssr-faq\">");
        foreach (var f in faqs) w.Tag("dt", f.Question).Tag("dd", f.Answer);
        w.Raw("</dl></section>");
    }

    private static void TripCards(HtmlWriter w, IReadOnlyList<TripSummaryDto> trips)
    {
        w.Raw("<ul class=\"ssr-cards\">");
        foreach (var t in trips)
        {
            w.Raw("<li>").Link(SeoSlug.TripPath(t.TripId, t.Title), t.Title).Raw("<div class=\"ssr-muted\">");
            var bits = new List<string>();
            if (t.DurationLabel is not null) bits.Add(t.DurationLabel);
            bits.Add($"From {TripPageContent.Rupees(t.AmountPerPerson)} per person");
            if (t.NextSlotStartDate is { } next) bits.Add("Next departure " + next.ToString("d MMM yyyy", India));
            w.Text(string.Join(" · ", bits));
            if (t.DestinationNames.Count > 0) w.Raw("<br>").Text(string.Join(", ", t.DestinationNames));
            w.Raw("</div></li>");
        }
        w.Raw("</ul>");
    }

    private static void DestinationCards(HtmlWriter w, IReadOnlyList<DestinationSummaryDto> destinations)
    {
        w.Raw("<ul class=\"ssr-cards\">");
        foreach (var d in destinations)
        {
            w.Raw("<li>").Link($"/destinations/{d.Slug}", d.Name).Raw("<div class=\"ssr-muted\">");
            if (!string.IsNullOrWhiteSpace(d.Tagline)) w.Text(d.Tagline).Raw("<br>");
            w.Text($"{d.TripCount} {(d.TripCount == 1 ? "trip" : "trips")}");
            if (d.StartingPrice is { } from) w.Text($" · from {TripPageContent.Rupees(from)}");
            w.Raw("</div></li>");
        }
        w.Raw("</ul>");
    }

    private string WhatsAppHref(string message) =>
        $"https://wa.me/{contactOptions.Value.WhatsAppNumber}?text={Uri.EscapeDataString(message)}";

    // ---------- Pages ----------

    private string HomeBody(
        IReadOnlyList<TripSummaryDto> trips,
        IReadOnlyList<DestinationSummaryDto> destinations,
        IReadOnlyList<TravelMomentDto> moments,
        IReadOnlyList<BlogPostSummaryDto> stories,
        IReadOnlyList<FaqItem> faqs) => Page(w =>
    {
        w.Raw("<h1>Meet. Travel. Explore. <span>").Text(HomeHeadingSub).Raw("</span></h1>")
         .Tag("p", HomeIntro);

        w.Raw("<section class=\"ssr-sec\" id=\"upcoming-trips\">").Tag("h2", "Upcoming trips");
        if (trips.Count == 0) w.Tag("p", "No upcoming trips are available right now. Please check back soon.", "ssr-muted");
        else TripCards(w, trips);
        w.Raw("</section>");

        if (destinations.Count > 0)
        {
            w.Raw("<section class=\"ssr-sec\">").Tag("h2", "Trending places");
            DestinationCards(w, destinations);
            w.Raw("</section>");
        }

        if (moments.Count > 0)
        {
            w.Raw("<section class=\"ssr-sec\">").Tag("h2", "Real Travel Moments").Raw("<ul class=\"ssr-cards\">");
            foreach (var m in moments)
            {
                w.Raw("<li>").Raw(Img(m.ImageUrl, m.Caption ?? "A Ghumo Odisha group trip"));
                if (m.Caption is not null) w.Tag("div", m.Caption, "ssr-muted");
                w.Raw("</li>");
            }
            w.Raw("</ul></section>");
        }

        if (stories.Count > 0)
        {
            w.Raw("<section class=\"ssr-sec\">").Tag("h2", "News & Blog: inspiring travel stories");
            StoryCards(w, stories);
            w.Raw("<p>").Link("/blog", "All travel stories").Raw("</p></section>");
        }

        HowBookingWorks(w);
        Faqs(w, "Frequently asked questions", faqs);
    });

    private const string HomeHeadingSub = TripPageContent.HomeHeadingSub;

    private static readonly string HomeIntro = TripPageContent.HomeIntro;

    private static void HowBookingWorks(HtmlWriter w)
    {
        w.Raw("<section class=\"ssr-sec\">").Tag("h2", "How booking works").Raw("<ol class=\"ssr-list\">");
        foreach (var step in TripPageContent.BookingSteps) w.Raw("<li><b>").Text(step.Question).Raw("</b> — ").Text(step.Answer).Raw("</li>");
        w.Raw("</ol></section>");
    }


    private string TripBody(TripDetailDto t, (string Name, string Path)[] breadcrumbs, IReadOnlyList<TripSummaryDto> related) => Page(w =>
    {
        var photo = t.Photos.FirstOrDefault();
        if (photo is not null)
        {
            w.Raw($"<img class=\"ssr-hero\" src=\"{Html.Encode(photo.ImageUrl)}\" alt=\"{Html.Encode(t.Title)}\" width=\"1280\" height=\"560\" fetchpriority=\"high\">");
        }
        w.Tag("h1", t.Title);
        w.Raw("<p>").Text($"From {TripPageContent.Rupees(t.AmountPerPerson)} per person").Raw("</p>");

        w.Raw("<ul class=\"ssr-facts\">");
        if (t.DurationLabel is not null) w.Raw("<li><span>Duration</span>").Text(t.DurationLabel.Replace(" / ", " · ")).Raw("</li>");
        var next = t.DateSlots.FirstOrDefault(s => !s.IsSoldOut && !s.IsBookingClosed) ?? t.DateSlots.FirstOrDefault();
        if (next is not null) w.Raw("<li><span>Next departure</span>").Text(next.StartDate.ToString("ddd, d MMM", India)).Raw("</li>");
        if (t.DepartureCity is not null) w.Raw("<li><span>Departs from</span>").Text(t.DepartureCity).Raw("</li>");
        if (t.PickupPoints.Count > 0) w.Raw("<li><span>Pickup from</span>").Text(t.PickupPoints[0].Location + (t.PickupPoints.Count > 1 ? $" +{t.PickupPoints.Count - 1} more" : "")).Raw("</li>");
        w.Raw("<li><span>Starting from</span>").Text($"{TripPageContent.Rupees(t.AmountPerPerson)} / person").Raw("</li></ul>");

        if (t.ItineraryDays.Count > 0)
        {
            w.Raw("<section class=\"ssr-sec\">").Tag("h2", "Day-wise itinerary")
             .Tag("p", $"{t.ItineraryDays.Count} day{(t.ItineraryDays.Count == 1 ? "" : "s")} · timings may shift slightly with weather and traffic.", "ssr-muted");
            foreach (var day in t.ItineraryDays)
            {
                w.Tag("h3", $"Day {day.DayNumber}: {day.Title}");
                if (!string.IsNullOrWhiteSpace(day.Description)) w.Tag("p", day.Description);
                if (day.Points.Count > 0)
                {
                    w.Raw("<ol class=\"ssr-list\">");
                    foreach (var p in day.Points) w.Raw("<li><time>").Text(p.Time).Raw("</time> — ").Text(p.Description).Raw("</li>");
                    w.Raw("</ol>");
                }
            }
            w.Raw("</section>");
        }

        if (t.Highlights.Count > 0)
        {
            w.Raw("<section class=\"ssr-sec\">").Tag("h2", "What you'll see");
            foreach (var h in t.Highlights) w.Tag("h3", h.PlaceName).Tag("p", h.Description);
            w.Raw("</section>");
        }

        if (t.PlacesCovered.Count > 0)
        {
            w.Raw("<section class=\"ssr-sec\">").Tag("h2", "Places covered").Raw("<ul class=\"ssr-list\">");
            foreach (var place in t.PlacesCovered) w.Tag("li", place);
            w.Raw("</ul></section>");
        }

        var included = TripPageContent.IncludedItems(t.Inclusions);
        if (included.Count > 0)
        {
            w.Raw("<section class=\"ssr-sec\">").Tag("h2", "What's included")
             .Tag("p", "Covered in the trip price — nothing extra to pay for these.", "ssr-muted").Raw("<ul class=\"ssr-list\">");
            foreach (var item in included) w.Tag("li", item);
            w.Raw("</ul></section>");
        }

        w.Raw("<section class=\"ssr-sec\">").Tag("h2", "Upcoming departures");
        if (t.DateSlots.Count == 0)
        {
            w.Tag("p", "No upcoming dates yet — check back soon.", "ssr-muted");
        }
        else
        {
            w.Raw("<ul class=\"ssr-list\">");
            foreach (var s in t.DateSlots)
            {
                w.Raw("<li>").Text($"{s.StartDate.ToString("ddd, d MMM yyyy", India)} → {s.EndDate.ToString("ddd, d MMM yyyy", India)}")
                 .Text(s.IsSoldOut || s.IsBookingClosed ? " · Seats filled" : "").Raw("</li>");
            }
            w.Raw("</ul>");
        }
        w.Raw("<p>").Raw($"<a class=\"ssr-cta\" href=\"{Html.Encode(WhatsAppHref($"Hi! I'd like to know more about the {t.Title} trip."))}\">").Text("Ask on WhatsApp").Raw("</a></p></section>");

        w.Raw("<section class=\"ssr-sec\">").Tag("h2", "About this trip").Tag("p", t.Description, "ssr-pre")
         .Tag("p", "Bigger groups always travel with both a lady coordinator and a gents coordinator.", "ssr-muted").Raw("</section>");

        if (t.Destinations.Count > 0)
        {
            w.Raw("<section class=\"ssr-sec\">").Tag("h2", "Destinations on this trip").Raw("<ul class=\"ssr-list\">");
            foreach (var d in t.Destinations) w.Raw("<li>").Link($"/destinations/{d.Slug}", $"{d.Name}: places, best time and trips").Raw("</li>");
            w.Raw("</ul></section>");
        }

        Faqs(w, "Good to know", t.Faqs);

        if (related.Count > 0)
        {
            w.Raw("<section class=\"ssr-sec\">").Tag("h2", "More trips you may like");
            TripCards(w, related);
            w.Raw("</section>");
        }
    }, breadcrumbs);

    private string DestinationBody(DestinationDetailDto d, IReadOnlyList<TripSummaryDto> trips, IReadOnlyList<FaqItem> faqs) => Page(w =>
    {
        if (d.HeroImageUrl is not null)
        {
            w.Raw($"<img class=\"ssr-hero\" src=\"{Html.Encode(d.HeroImageUrl)}\" alt=\"{Html.Encode(d.Name)}\" width=\"1280\" height=\"560\" fetchpriority=\"high\">");
        }
        if (!string.IsNullOrWhiteSpace(d.Region)) w.Tag("p", d.Region, "ssr-muted");
        w.Tag("h1", d.Name);
        if (!string.IsNullOrWhiteSpace(d.Tagline)) w.Tag("p", d.Tagline);

        w.Raw("<section class=\"ssr-sec\">").Tag("h2", $"Trips in {d.Name}");
        if (trips.Count == 0) w.Tag("p", $"No trips to {d.Name} are scheduled right now.", "ssr-muted");
        else TripCards(w, trips);
        w.Raw("</section>");

        if (!string.IsNullOrWhiteSpace(d.AboutText))
        {
            w.Raw("<section class=\"ssr-sec\">").Tag("h2", $"About {d.Name}").Tag("p", d.AboutText, "ssr-pre").Raw("<ul class=\"ssr-facts\">");
            if (!string.IsNullOrWhiteSpace(d.BestSeason)) w.Raw("<li><span>Best season</span>").Text(d.BestSeason).Raw("</li>");
            if (!string.IsNullOrWhiteSpace(d.DistanceFromBhubaneswar)) w.Raw($"<li><span>From {TripPageContent.HomeCity}</span>").Text(d.DistanceFromBhubaneswar).Raw("</li>");
            if (!string.IsNullOrWhiteSpace(d.IdealDuration)) w.Raw("<li><span>Ideal duration</span>").Text(CleanDuration(d.IdealDuration)).Raw("</li>");
            if (!string.IsNullOrWhiteSpace(d.KnownFor)) w.Raw("<li><span>Known for</span>").Text(d.KnownFor).Raw("</li>");
            w.Raw("</ul></section>");
        }

        Faqs(w, $"{d.Name} travel FAQs", faqs);
        w.Raw("<p>").Link("/#upcoming-trips", "See all trips").Raw("</p>");
    }, [(d.Name, $"/destinations/{d.Slug}")]);

    private string TermsBody() => Page(w =>
    {
        w.Tag("h1", "Trip Terms & Conditions").Tag("p", TermsAndConditionsContent.Text, "ssr-pre");
    }, [("Terms & Conditions", "/terms")]);

    private string NotFoundBody(IReadOnlyList<DestinationSummaryDto> destinations) => Page(w =>
    {
        w.Tag("h1", "Page not found")
         .Tag("p", "The page you're looking for doesn't exist or has moved.")
         .Raw("<p>").Link("/", "Back to home").Text(" · ").Link("/#upcoming-trips", "Browse all trips").Raw("</p>");
        if (destinations.Count > 0)
        {
            w.Tag("h2", "Popular destinations");
            DestinationCards(w, destinations);
        }
    });
}
