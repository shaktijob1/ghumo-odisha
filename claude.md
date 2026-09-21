# Ghumo Odisha

Production travel booking platform for a real business. Not a demo — every
rule below is enforced by the backend and must actually work end to end
against a real MySQL database.

## Reference docs — read these first, every session

- `docs/architecture.md` — approved Phase 1 architecture: ER diagram, API
  map, Angular routes, admin/customer page structure, booking lifecycle,
  concurrency strategy, auth flow, folder structure, phase order.
- `docs/ui-reference.html` — approved visual design (Variation B: modern
  minimal). Open it in a browser to see every screen at laptop and mobile
  widths. This is the **visual spec** — colors, spacing, component shapes,
  layout breakpoints — not code to copy. Build it as proper Angular
  standalone components, not by lifting the raw markup.

Read both fully before writing any code, and re-check them whenever a task
touches a screen or endpoint they describe. If something in a prompt
conflicts with these docs, flag the conflict instead of silently picking
one.

## Stack

- **Frontend:** Angular 19+, TypeScript, standalone components, Angular
  Router, Reactive Forms, HttpClient, signals/observables, lazy-loaded
  feature areas, environment-based API config.
- **Backend:** ASP.NET Core .NET 10 Web API, C#, EF Core, MySQL 8+ via
  Pomelo.EntityFrameworkCore.MySql, JWT auth with Admin/Customer roles,
  DI, DTOs, service layer, FluentValidation, structured logging.
- **Database:** MySQL 8+, EF Core migrations, foreign keys, indexes,
  constraints, transaction-safe seat management.
- **Testing:** Playwright for E2E and real screenshots (desktop 1440×900,
  mobile 390×844), backend/API tests, `dotnet build` and `ng build` must
  both pass clean before any phase is considered done.

## Visual direction — Variation B (locked in)

```
--bg #FFFFFF   --surface #FFFFFF  --ink #0F1416   --muted #6A7478
--line #E7E9EA --accent #0F6F5C   --accent-ink #FFFFFF
--ok #0F6F5C   --wait #9A6A11     --danger #C0483A
radius: 14px card / 12px control / 999px pill
type: Inter, weights 600 / 500 / 400
```

White ground, hairline `#E7E9EA` borders, rounded cards, one accent color.
Admin reuses the same tokens on a `#F7F8F8` canvas, denser spacing. Do not
introduce a different palette, font, or corner-radius system without being
asked — match `docs/ui-reference.html` exactly.

## The one rule that matters most

**A booking REQUEST never decreases seats. Only an admin CONFIRM decreases
seats, inside a database transaction.**

Flow: customer picks a date + seats → backend creates `Booking` with
`BookingStatus = Requested` → `AvailableSeats` is untouched → WhatsApp
opens with a prefilled message → organizer contacts customer → customer
pays an advance offline → admin opens the booking, enters the advance,
clicks Confirm → **only then** does the backend deduct seats.

Confirm, inside one transaction:
1. Load booking; reject unless status is `Requested` or `Pending`.
2. `SELECT ... FOR UPDATE` the `TripDateSlot` row.
3. Recompute `TotalAmount` from `Trip.AmountPerPerson` in the database —
   never trust a price or seat count sent from Angular.
4. Validate `0 ≤ AdvanceAmount ≤ TotalAmount`.
5. Guarded deduct:
   ```sql
   UPDATE TripDateSlots
      SET AvailableSeats = AvailableSeats - @seats
    WHERE TripDateSlotId = @slotId AND AvailableSeats >= @seats;
   ```
   If affected rows ≠ 1 → throw, roll back, booking stays `Requested`.
6. Set `Confirmed`, `PaymentStatus`, `ConfirmedAt`, amounts. Commit.

Cancel restores seats via `LEAST(AvailableSeats + @seats, TotalSeats)`,
guarded by requiring `BookingStatus = Confirmed` first — so a repeat
cancel is rejected on status, not arithmetic, and never double-restores.

Manual admin bookings (phone/offline) go through this exact same service
method. Never write a second seat-deduction code path.

Full detail: `docs/architecture.md` §10.

## Hard rules — never violate these

- Never trust price or seat count from the frontend. Recompute server-side.
- Never let Angular confirm a booking or touch `AvailableSeats` directly.
- Never store a plaintext PIN. Hash it (PBKDF2 via ASP.NET Core
  `PasswordHasher`), never return the hash, never log the PIN.
- Never let a customer access another customer's bookings or profile —
  read the id from the JWT claim, never from a route/body parameter.
- Never hardcode the WhatsApp number or API URLs in more than one place —
  route through `OrganizerContactOptions` / `environment.apiUrl`.
- Never expose stack traces to the client; log them server-side only.
- Never skip DTOs — don't return EF entities directly from controllers.
- Never leave `ng build` or `dotnet build` broken between phases.

## Build in phases — do not skip ahead

Follow the order in `docs/architecture.md` §14:

1. ~~Architecture~~ (done — see `docs/architecture.md`)
2. Backend foundation: entities, DbContext, EF configurations, initial
   migration, seed data (Koraput Escape, Puri Konark Escape, Mahendragiri
   Adventure — each with photos, highlights, multiple date slots,
   itinerary, room photos)
3. Auth: JWT, admin login, customer phone+PIN register/login, lockout,
   guards, interceptor
4. Trip management: CRUD, photos, highlights, date slots, itinerary,
   room photos, image upload
5. Booking: request, confirm, reject, cancel, manual admin booking,
   concurrency protection
6. Admin frontend: all ten pages
7. Customer frontend: Variation B, all pages
8. Wire Angular to the live API — remove every placeholder/mock array
9. Testing: `dotnet build`, `ng build`, API tests, Playwright critical
   flows (oversell test, cancellation test, price-manipulation test)
10. Real screenshots from the running app into `ui-snapshots/`
11. Fix everything the screenshots reveal, finish the README

After finishing a phase: run the relevant build, fix every error, then
stop and summarize what was built before moving to the next phase. Don't
dump multiple phases into one uninterrupted pass.

## Conventions

- Controllers stay thin; business logic lives in the service layer
  (`TripService`, `BookingService`, `CustomerService`, etc. — full list
  in `docs/architecture.md` §6).
- API responses use the `{ success, message, data }` / `{ success,
  message, errors }` envelope everywhere.
- Angular: one feature area per lazy-loaded route bundle
  (`features/trips`, `features/admin`, etc.), reusable components in
  `shared/` (TripCard, SeatSelector, StatusBadge, ConfirmDialog, Toast,
  Loading/Empty/Error states — see architecture.md §6/§7).
- Every API-driven screen needs Loading, Empty, Error, and Success states.
- Money: `decimal(10,2)`. Timestamps: UTC. Enums: persisted as `int`.

## Commands

```bash
# backend
cd backend && dotnet restore
dotnet ef migrations add <Name> --project GhumoOdisha.Infrastructure
dotnet ef database update
dotnet run --project GhumoOdisha.Api

# frontend
cd frontend && npm install
ng serve

# tests
dotnet test
npx playwright test
```

## When something is ambiguous

Check `docs/architecture.md` first — it answers most structural questions
(routes, entities, endpoint shapes). If it's a visual question, check
`docs/ui-reference.html`. If neither resolves it, make the most reasonable
call consistent with the rules above, note the assumption in your summary,
and keep going rather than stalling on a clarifying question.