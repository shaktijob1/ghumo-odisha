# SEO checklist — ghumoodisha.com

The website side is done in code (per-page titles and descriptions, WhatsApp/Facebook previews,
structured data, `sitemap.xml`, `robots.txt`, readable trip links, faster page loads). The steps
below are the ones only the business owner can do. Do them in this order.

---

## 1. After deploying — check it works (10 min)

1. Open `https://ghumoodisha.com/robots.txt` and `https://ghumoodisha.com/sitemap.xml`. Both should
   load. The sitemap should list your trips and destinations.
2. Paste a trip link into **WhatsApp** (send it to yourself). You should see the trip photo, the
   title with price, and a short description.
   - WhatsApp caches previews. If an old, empty preview shows, add `?v=1` to the end of the link
     once to force a fresh one.
3. Check the same link in Facebook's **Sharing Debugger**: https://developers.facebook.com/tools/debug/
   → paste the link → **Scrape Again**.
4. Check Google can read the trip details: https://search.google.com/test/rich-results → paste a
   trip link. It should find **TouristTrip** and **BreadcrumbList** with no errors.
5. Make sure the site has **one** address. Pick either `ghumoodisha.com` or `www.ghumoodisha.com`,
   and set your hosting/DNS to redirect the other one to it (and `http://` to `https://`). The
   site's code uses `https://ghumoodisha.com` — if you choose `www`, change `Seo:SiteUrl` in
   `backend/GhumoOdisha.Api/appsettings.json` and `siteUrl` in `frontend/src/environments/*.ts`.

---

## 2. Google Search Console (20 min) — most important

This tells Google your site exists and shows you what people search to find you.

1. Go to https://search.google.com/search-console and sign in with the business Google account.
2. **Add property** → choose **Domain** → type `ghumoodisha.com`.
3. Google gives you a **TXT record**. Add it in your domain registrar's DNS settings (GoDaddy,
   Hostinger, etc. → DNS → Add record → Type TXT, Host `@`, Value = what Google gave). Click
   **Verify** (it can take up to an hour).
4. Left menu → **Sitemaps** → enter `sitemap.xml` → **Submit**.
5. Left menu → **URL Inspection** → paste your home page, then each main trip link →
   **Request indexing**. (Do this again whenever you add a new trip.)

Check back after 1–2 weeks: **Performance** shows which searches bring people in.

## 3. Bing Webmaster Tools (5 min)

Bing also powers other search engines and some AI assistants.

1. https://www.bing.com/webmasters → sign in.
2. Choose **Import from Google Search Console**. It copies your site and sitemap automatically.

---

## 4. Google Business Profile (30 min) — biggest win for local searches

This puts you on Google Maps and in "travel agency near me" / "Odisha tour packages" results.

1. Go to https://business.google.com → **Add your business**.
2. Fill in:

| Field | What to enter |
|---|---|
| Business name | Ghumo Odisha |
| Primary category | **Tour agency** |
| Additional categories | Travel agency, Tour operator |
| Address | B55, DreamVilla, Bhubaneswar, Odisha 751024 (or tick "I deliver goods and services to my customers" and hide the address if it's a home office) |
| Service area | Odisha |
| Phone | +91 80937 31041 |
| Email | booking@ghumoodisha.com |
| Website | https://ghumoodisha.com |
| Hours | Your real enquiry hours |

3. **Description** (paste, max 750 characters):

   > Ghumo Odisha runs group trips and tour packages across Odisha — Puri, Konark, Chilika,
   > Satapada, Koraput, Mahendragiri and more. Every trip has fixed departure dates, AC travel,
   > comfortable stays and a trip coordinator who travels with the group, so you can simply show
   > up and enjoy. Book your seat online for just ₹99 per seat and pay the rest before the trip.
   > Perfect for solo travellers, friends and families who want to explore Odisha's temples,
   > beaches, lakes, waterfalls and hills with a fun, well-organised group.

4. Verify the business (Google sends a code by phone, email, video or post).
5. Add **at least 10 photos**: trips, group photos, the vehicle, stays, your logo as the profile
   photo, and a cover photo.
6. Under **Products**/**Services**, add each trip with its price and a link to its page.
7. Post an **Update** whenever you open new dates (e.g. "Puri Konark trip — October dates open").

### Getting reviews (the #1 ranking factor for local search)

After each trip, send this on WhatsApp (replace the link with your "Ask for reviews" link from
the Business Profile dashboard):

> Hi {name}! Thank you for travelling with Ghumo Odisha 🙏 We hope you loved the trip. If you have
> a minute, a quick Google review would help us a lot: {review link}

Reply to every review, good or bad.

---

## 5. Social profiles

Create (or use your existing) Instagram, Facebook and YouTube pages. Put
`https://ghumoodisha.com` in each bio. Then add the profile links to the site so Google connects
them to your business — in `backend/GhumoOdisha.Api/appsettings.json`:

Instagram is already added. Add the others alongside it:

```json
"Seo": {
  "SameAs": [
    "https://www.instagram.com/ghumo__odisha/",
    "https://www.facebook.com/your-page",
    "https://www.youtube.com/@your-channel"
  ]
}
```

---

## 6. Content that brings visitors (ongoing)

Google ranks pages that answer what people search. Two things help most:

**a. Fill in every destination page fully** (Admin → Destinations): About text (150+ words), Best
season, Known for, Ideal duration, Distance from Bhubaneswar, and a good hero photo. Each
destination page is its own chance to rank for "{place} tour package".

**b. Write trip descriptions for people, not just a list.** Mention the places by name, the
season, what's included, and who it's for. Around 150–300 words per trip.

Topics people search in Odisha that could become guides later (each linking to your matching trip):

- Best time to visit Koraput / Daringbadi / Chilika
- Puri Konark 2-day itinerary from Bhubaneswar
- Chilika dolphin watching — Satapada boat ride guide
- Weekend trips from Bhubaneswar
- Odisha trip for solo travellers — is a group trip safe?
- Mahendragiri trek guide

---

## 7. Other places to list the business (links from other sites help ranking)

- **Justdial** and **Sulekha** (free listings; many Odisha travellers search there)
- **TripAdvisor** — list as a tour operator; ask happy customers to review there too
- **Odisha Tourism** registered operators list, if you register with the state tourism department
- Your partner/influencer coupon holders — ask them to put your trip link in their bio and posts

---

## Settings you can change later

All in `backend/GhumoOdisha.Api/appsettings.json` under `"Seo"`:

| Setting | What it does |
|---|---|
| `DefaultDescription` | Home page description in Google and link previews |
| `DefaultImage` | Preview photo for pages without their own (e.g. `/uploads/hero/xyz.jpg`). The home page already uses the home banner photo you upload in admin. |
| `SameAs` | Your social profile links (see step 5) |
| Address fields | The address Google shows for the business |

If you change the home page wording, also change `DEFAULT_DESCRIPTION` in
`frontend/src/app/core/services/seo.service.ts` so both say the same thing.
