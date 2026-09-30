import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, OnDestroy, OnInit, computed, effect, inject, signal, viewChild, viewChildren } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Location } from '@angular/common';
import { SeoService } from '../../core/services/seo.service';
import { durationLabel, parseTripId, tripPath } from '../../shared/utils/trip-path';
import { PublicTripService } from '../../core/services/public-trip.service';
import { CustomerBookingService } from '../../core/services/customer-booking.service';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { CustomerProfileService } from '../../core/services/customer-profile.service';
import { ContactService } from '../../core/services/contact.service';
import { ToastService } from '../../core/services/toast.service';
import { TermsDialogService } from '../../core/services/terms-dialog.service';
import { TripDetail, DateSlot, TripHighlight, TripInclusions } from '../../core/models/trip.model';
import { BookingResponse, CreateBookingResult } from '../../core/models/booking.model';
import { CustomerAuthResponse } from '../../core/models/auth.model';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { SeatSelectorComponent } from '../../shared/components/seat-selector.component';
import { WhatsappAuthComponent } from '../../shared/components/whatsapp-auth.component';
import { PaymentPanelComponent } from '../../shared/components/payment-panel.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { downloadFile } from '../../shared/utils/download-file';
import { toLocalDateKey } from '../../shared/utils/date-key';
import { payNowFor } from '../../shared/components/booking-price-summary.component';
import { CouponSelection } from '../../shared/components/coupon-field.component';
import { roomsForSeats } from '../../shared/utils/rooms';
import { scrollRowBy } from '../../shared/utils/scroll-row';

type PhotoRow = 'hl' | 'stay' | 'travel';

// Booking popup: confirm date → confirm seats (rooms shown) → then the 'flow' chain of
// pickup point / sign-in / name / create request → trip summary with coupon + Pay Now.
type BookingStep = 'date' | 'seats' | 'flow';

type LoadState = 'loading' | 'ready' | 'error';

type InclusionKey = keyof TripInclusions;


const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

@Component({
  selector: 'app-trip-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent, SeatSelectorComponent, WhatsappAuthComponent, PaymentPanelComponent, ImageUrlPipe],
  templateUrl: './trip-detail.component.html',
  styleUrl: './trip-detail.component.css',
})
export class TripDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly location = inject(Location);
  private readonly seo = inject(SeoService);
  private readonly tripService = inject(PublicTripService);
  private readonly bookingService = inject(CustomerBookingService);
  readonly auth = inject(CustomerAuthService);
  private readonly contactService = inject(ContactService);
  private readonly profileService = inject(CustomerProfileService);
  private readonly toast = inject(ToastService);
  readonly termsDialog = inject(TermsDialogService);

  readonly contact = this.contactService.get();
  readonly state = signal<LoadState>('loading');
  readonly trip = signal<TripDetail | null>(null);

  readonly heroIndex = signal(0);
  readonly selectedSlotId = signal<number | null>(null);
  /** Travellers are chosen as gents + ladies (the trip keeps a 1:1 mix); seats are their total. */
  readonly gents = signal(1);
  readonly ladies = signal(0);
  readonly seats = computed(() => this.gents() + this.ladies());
  // Chosen in the sidebar; carried into the payment step, where it is (re)validated for this customer.
  readonly coupon = signal<CouponSelection | null>(null);
  // A coupon's amount is per seat (₹200 × 4 seats = ₹800 off) — same as BookingPaymentService server-side.
  readonly couponDiscount = computed(() => (this.coupon()?.validated ? this.coupon()!.discountAmount * this.seats() : 0));
  readonly submitting = signal(false);
  readonly submitError = signal<string | null>(null);
  readonly bookingResult = signal<CreateBookingResult | null>(null);
  readonly showAuthModal = signal(false);

  readonly paymentConfirmed = signal(false);
  readonly confirmedBooking = signal<BookingResponse | null>(null);
  readonly downloadingInvoice = signal(false);

  readonly selectedPickupPointId = signal<number | null>(null);

  readonly showNamePrompt = signal(false);
  readonly namePromptValue = signal('');
  readonly namePromptSubmitting = signal(false);
  readonly namePromptError = signal<string | null>(null);

  readonly selectedHighlight = signal<TripHighlight | null>(null);
  readonly selectedDayIndex = signal(0);
  readonly showAllSlots = signal(false);

  private static readonly VISIBLE_SLOT_COUNT = 3;

  // Slots arrive already sorted by StartDate from the API; past departures are dropped here so
  // the month tabs and cards never offer a date that has already gone.
  readonly upcomingSlots = computed(() => {
    const today = toLocalDateKey(new Date());
    return (this.trip()?.dateSlots ?? []).filter((s) => s.startDate.slice(0, 10) >= today);
  });

  // One tab per calendar month that has at least one upcoming departure, in chronological order.
  readonly slotMonths = computed(() => {
    const months: { key: string; label: string }[] = [];
    for (const s of this.upcomingSlots()) {
      const key = s.startDate.slice(0, 7);
      if (months.at(-1)?.key !== key) {
        months.push({ key, label: MONTH_NAMES[Number(key.slice(5, 7)) - 1] });
      }
    }
    return months;
  });

  readonly selectedMonth = signal<string | null>(null);

  readonly monthSlots = computed(() => {
    const month = this.selectedMonth();
    return this.upcomingSlots().filter((s) => s.startDate.startsWith(month ?? ''));
  });

  readonly visibleSlots = computed(() => {
    const slots = this.monthSlots();
    return this.showAllSlots() ? slots : slots.slice(0, TripDetailComponent.VISIBLE_SLOT_COUNT);
  });

  private heroTimer?: ReturnType<typeof setInterval>;

  // Day tabs only change when the customer picks one — no auto-advance.
  private readonly dayTabEls = viewChildren<ElementRef<HTMLButtonElement>>('dayTab');
  private readonly dayTabsEl = viewChild<ElementRef<HTMLDivElement>>('dayTabsEl');

  tripId!: number;
  private clientRequestId: string | null = null;
  // The date/seats/pickup the current booking request was created with.
  private lastRequest: { slotId: number; seats: number; gents: number; pickupId: number | null } | null = null;
  // An unpaid request set aside when the popup was closed or "Edit details" was used.
  private draft: { result: CreateBookingResult; slotId: number; seats: number; gents: number; pickupId: number | null } | null = null;
  private justAuthenticated = false;

  readonly selectedSlot = computed<DateSlot | null>(() => {
    const slots = this.trip()?.dateSlots ?? [];
    return slots.find((s) => s.tripDateSlotId === this.selectedSlotId()) ?? null;
  });

  readonly includedItems = computed(() => {
    const inc = this.trip()?.inclusions;
    if (!inc) return [];
    const items: { key: InclusionKey; label: string; caption: string }[] = [];
    if (inc.breakfast) items.push({ key: 'breakfast', label: 'Breakfast included', caption: 'Breakfast' });
    if (inc.lunch) items.push({ key: 'lunch', label: 'Lunch included', caption: 'Lunch' });
    if (inc.dinner) items.push({ key: 'dinner', label: 'Dinner included', caption: 'Dinner' });
    if (inc.stay) items.push({ key: 'stay', label: 'AC room included', caption: 'AC Room' });
    if (inc.acVehicle) items.push({ key: 'acVehicle', label: 'AC vehicle included', caption: 'AC Vehicle' });
    if (inc.pushbackVehicle) items.push({ key: 'pushbackVehicle', label: 'Pushback vehicle included', caption: 'Pushback Vehicle' });
    if (inc.camping) items.push({ key: 'camping', label: 'Camping included', caption: 'Camping' });
    if (inc.bonfire) items.push({ key: 'bonfire', label: 'Bonfire included', caption: 'Bonfire' });
    if (inc.musicalNight) items.push({ key: 'musicalNight', label: 'Musical night included', caption: 'Musical Night' });
    if (inc.swimmingPool) items.push({ key: 'swimmingPool', label: 'Swimming pool included', caption: 'Swimming Pool' });
    if (inc.coordinator) items.push({ key: 'coordinator', label: 'Trip coordinator included', caption: 'Coordinator' });
    return items;
  });

  readonly totalAmount = computed(() => (this.trip()?.amountPerPerson ?? 0) * this.seats());

  // What's actually collected via Razorpay to confirm the booking — ₹99/seat, same rate the
  // payment panel charges once the booking request exists. Never used to compute what's actually
  // charged; that's recomputed server-side from scratch.
  readonly bookingAdvance = computed(() => payNowFor(this.trip()?.amountPerPerson ?? 0, this.seats(), this.couponDiscount()));

  /** Booking advance for one seat — what the "PAY ₹99 & CONFIRM BOOKING" button shows before seats are chosen. */
  get perSeatAdvance(): number {
    return payNowFor(this.trip()?.amountPerPerson ?? 0, 1, 0);
  }

  readonly requiresPickupPoint = computed(() => (this.trip()?.pickupPoints.length ?? 0) > 0);

  readonly showBookingModal = signal(false);
  readonly bookingStep = signal<BookingStep>('date');
  // Captured when the popup opens so the step bar keeps its "Sign in" step after sign-in succeeds.
  private readonly signInStepShown = signal(false);

  readonly roomsAllotted = computed(() => roomsForSeats(this.seats()));

  /** Travellers per room, spread as evenly as possible (5 seats → 2 rooms of 3 + 2). */
  readonly roomPlan = computed(() => {
    const seats = this.seats();
    const rooms = this.roomsAllotted();
    if (rooms === 0) return [];
    const base = Math.floor(seats / rooms);
    const extra = seats % rooms;
    return Array.from({ length: rooms }, (_, i) => {
      const guests = base + (i < extra ? 1 : 0);
      return { guests, icons: Array.from({ length: guests }, (_, g) => g) };
    });
  });

  readonly bookingSteps = computed(() => {
    const steps: { key: 'date' | 'seats' | 'pickup' | 'signin' | 'summary'; label: string }[] = [
      { key: 'date', label: 'Date' },
      { key: 'seats', label: 'Seats' },
    ];
    if (this.requiresPickupPoint()) steps.push({ key: 'pickup', label: 'Pickup' });
    if (this.signInStepShown()) steps.push({ key: 'signin', label: 'Sign in' });
    steps.push({ key: 'summary', label: 'Summary' });
    return steps;
  });

  readonly activeStepIndex = computed(() => {
    const step = this.bookingStep();
    let key: string;
    if (step !== 'flow') key = step;
    else if (this.needsPickupSelection()) key = 'pickup';
    else if (this.showAuthModal() || this.showNamePrompt()) key = 'signin';
    else key = 'summary';
    return Math.max(0, this.bookingSteps().findIndex((s) => s.key === key));
  });

  // The pickup step stays open until "Continue" confirms a choice — picking a point alone doesn't advance.
  readonly pickupConfirmed = signal(false);
  readonly needsPickupSelection = computed(
    () => this.requiresPickupPoint() && !(this.pickupConfirmed() && this.selectedPickupPointId()) && !this.bookingResult(),
  );

  ngOnInit(): void {
    this.tripId = parseTripId(this.route.snapshot.paramMap.get('id'));
    this.load();
  }

  ngOnDestroy(): void {
    this.stopHeroAutoplay();
  }

  load(): void {
    this.state.set('loading');
    this.tripService.getTrip(this.tripId).subscribe({
      next: (t) => {
        this.trip.set(t);
        this.applySeo(t);
        const firstOpenSlot = this.upcomingSlots().find((s) => !s.isSoldOut && !s.isBookingClosed);
        this.selectedSlotId.set(firstOpenSlot?.tripDateSlotId ?? null);
        // Open on the month holding the next bookable departure (e.g. October if September is
        // empty or sold out), falling back to the first month that has any departure at all.
        this.selectedMonth.set(firstOpenSlot?.startDate.slice(0, 7) ?? this.slotMonths()[0]?.key ?? null);
        // No pickup point is pre-selected — choosing one is now an explicit step in the booking
        // popup, not a silently-applied default the customer might not notice.
        this.resetPickup();
        this.selectedDayIndex.set(0);
        this.showAllSlots.set(false);
        this.state.set('ready');
        this.startHeroAutoplay();
      },
      error: () => this.state.set('error'),
    });
  }

  // Same title/description the API writes into the first page load (SeoPageRenderer.TripPage),
  // plus swapping an old /trips/1 address for the readable /trips/1-puri-konark one in place.
  private applySeo(t: TripDetail): void {
    const path = tripPath(t.tripId, t.title);
    const [currentPath, query] = this.location.path(false).split('?');
    if (currentPath !== path) {
      this.location.replaceState(path, query ?? '');
    }

    const today = toLocalDateKey(new Date());
    const next = t.dateSlots.filter((s) => s.startDate.slice(0, 10) >= today).sort((a, b) => a.startDate.localeCompare(b.startDate))[0];
    const duration = next ? ` – ${durationLabel(next.startDate, next.endDate)}` : '';
    const price = `₹${t.amountPerPerson.toLocaleString('en-IN')}`;
    const summary = `${t.title}${duration} group trip from ${price} per person. ${t.description.replace(/\s+/g, ' ').trim()}`;
    this.seo.setPage({
      title: `${t.title}${duration} from ${price} | Ghumo Odisha`,
      description: summary.length > 160 ? summary.slice(0, summary.lastIndexOf(' ', 159)) + '…' : summary,
      path,
      image: [...t.photos].sort((a, b) => a.displayOrder - b.displayOrder)[0]?.imageUrl ?? null,
    });
  }

  private startHeroAutoplay(): void {
    this.stopHeroAutoplay();
    if ((this.trip()?.photos.length ?? 0) > 1) {
      this.heroTimer = setInterval(() => this.nextHero(), 4500);
    }
  }

  private stopHeroAutoplay(): void {
    if (this.heroTimer) {
      clearInterval(this.heroTimer);
      this.heroTimer = undefined;
    }
  }




  // ---------- Itinerary agenda helpers ----------

  /** "8:00 AM" → { main: "8:00", suffix: "AM" }. Free text that doesn't look like a time is shown as-is. */
  splitTime(time: string): { main: string; suffix: string } {
    const m = /^\s*(\d{1,2}(?::\d{2})?)\s*([ap]\.?m\.?)?\s*$/i.exec(time ?? '');
    return m ? { main: m[1], suffix: (m[2] ?? '').replace(/\./g, '').toUpperCase() } : { main: time, suffix: '' };
  }

  /** Picks an icon for an itinerary activity from keywords in its text (order matters: first match wins). */
  activityKind(text: string): 'travel' | 'fun' | 'food' | 'temple' | 'water' | 'stay' | 'spot' {
    const t = (text ?? '').toLowerCase();
    if (/pick ?up|drop|depart|bus|drive|transfer|return|arriv|reach|travel/.test(t)) return 'travel';
    if (/bonfire|danc|music|party|fun|game|celebrat/.test(t)) return 'fun';
    if (/breakfast|lunch|dinner|tea|snack|meal|food|cook/.test(t)) return 'food';
    if (/temple|jagannath|shrine|church|monaster|heritage|museum|fort/.test(t)) return 'temple';
    if (/boat|beach|dolphin|lake|river|waterfall|swim|sea|island/.test(t)) return 'water';
    if (/check ?-?in|check ?-?out|stay|hotel|resort|camp|night|sleep|rest/.test(t)) return 'stay';
    return 'spot';
  }

  /** ‹ › on the day strip: shown only while there are more days off-screen that way. */
  private readonly reducedMotion = typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  readonly dayRailPrev = signal(false);
  readonly dayRailNext = signal(false);

  updateDayRail(): void {
    const el = this.dayTabsEl()?.nativeElement;
    if (!el) return;
    this.dayRailPrev.set(el.scrollLeft > 4);
    this.dayRailNext.set(el.scrollLeft + el.clientWidth < el.scrollWidth - 4);
  }

  /** Pages the day strip by most of its visible width. */
  scrollDays(direction: -1 | 1): void {
    const el = this.dayTabsEl()?.nativeElement;
    if (!el) return;
    el.scrollBy({ left: direction * el.clientWidth * 0.8, behavior: this.reducedMotion ? 'auto' : 'smooth' });
  }

  /** Previous / next day buttons under the day's plan; the page stays on the itinerary. */
  stepDay(direction: -1 | 1): void {
    const days = this.trip()?.itineraryDays.length ?? 0;
    const next = this.selectedDayIndex() + direction;
    if (next < 0 || next >= days) return;
    this.selectDay(next);
    const section = this.dayTabsEl()?.nativeElement.closest('.tdsec') as HTMLElement | null;
    // Bring the day's heading back into view when the visitor was reading further down.
    if (section && section.getBoundingClientRect().top < 0) {
      window.scrollTo({ top: window.scrollY + section.getBoundingClientRect().top - 72, behavior: this.reducedMotion ? 'auto' : 'smooth' });
    }
  }

  /** Shows the chosen day and scrolls its tab into view within the tab strip. */
  selectDay(index: number): void {
    this.selectedDayIndex.set(index);

    // Scrolls only the tab strip itself — never scrollIntoView, which would also move the page.
    const container = this.dayTabsEl()?.nativeElement;
    const btn = this.dayTabEls()[index]?.nativeElement;
    if (container && btn) {
      const target = btn.offsetLeft - (container.clientWidth - btn.clientWidth) / 2;
      container.scrollTo({ left: target, behavior: 'smooth' });
    }
  }

  /** Only marks the choice — the visitor moves on with "Continue" (or goes back to seats). */
  selectPickupPoint(id: number): void {
    this.selectedPickupPointId.set(id);
  }

  confirmPickup(): void {
    if (!this.selectedPickupPointId()) return;
    this.pickupConfirmed.set(true);
    this.tryProceed();
  }

  private resetPickup(): void {
    this.selectedPickupPointId.set(null);
    this.pickupConfirmed.set(false);
  }

  selectMonth(key: string): void {
    this.selectedMonth.set(key);
    this.showAllSlots.set(false);
  }

  selectSlot(slot: DateSlot): void {
    if (slot.isSoldOut || slot.isBookingClosed) return;
    this.selectedSlotId.set(slot.tripDateSlotId);
    this.resetTravellers(slot);
  }

  /** One traveller to start with — a gent, or a lady when the gents' places on this date are full. */
  private resetTravellers(slot: DateSlot | null): void {
    const gentsOpen = !slot || slot.gentsLeft > 0;
    this.gents.set(gentsOpen ? 1 : 0);
    this.ladies.set(gentsOpen ? 0 : 1);
  }

  /** Most gents / ladies this booking can add: their own open places, and never past the seats left. */
  readonly gentsMax = computed(() => {
    const s = this.selectedSlot();
    return s ? Math.max(0, Math.min(s.gentsLeft, s.availableSeats - this.ladies())) : 0;
  });
  readonly ladiesMax = computed(() => {
    const s = this.selectedSlot();
    return s ? Math.max(0, Math.min(s.ladiesLeft, s.availableSeats - this.gents())) : 0;
  });

  private nextHero(): void {
    const total = this.trip()?.photos.length ?? 0;
    if (total === 0) return;
    this.heroIndex.set((this.heroIndex() + 1) % total);
  }

  // Demo only — no PDF generation wired up yet.
  downloadItineraryDemo(): void {
    this.toast.info("Itinerary PDF download is coming soon — we'll notify you when it's ready.");
  }

  openHighlight(h: TripHighlight): void {
    this.selectedHighlight.set(h);
  }

  /** Position of the open place in "What you'll see", in the same order as the cards on the page. */
  readonly highlightIndex = computed(() => {
    const open = this.selectedHighlight();
    return open ? (this.trip()?.highlights ?? []).findIndex((h) => h.tripHighlightId === open.tripHighlightId) : -1;
  });

  /** Back / next inside the open place's popup; wraps around at either end. */
  stepHighlight(direction: 1 | -1): void {
    const places = this.trip()?.highlights ?? [];
    const at = this.highlightIndex();
    if (places.length < 2 || at < 0) return;
    this.selectedHighlight.set(places[(at + direction + places.length) % places.length]);
  }

  // ---------- "About this trip" key facts (all from the trip's own data) ----------

  /** "Pickup from … +3 more" opens the full list of pickup points. */
  readonly showPickups = signal(false);

  encode(value: string): string {
    return encodeURIComponent(value);
  }

  readonly tripFacts = computed(() => {
    const t = this.trip();
    if (!t) return [];
    const facts: { icon: 'clock' | 'calendar' | 'pin' | 'tag'; label: string; value: string }[] = [];
    const today = new Date().toISOString().slice(0, 10);
    const next = [...t.dateSlots]
      .filter((s) => !s.isSoldOut && !s.isBookingClosed && s.startDate >= today)
      .sort((a, b) => a.startDate.localeCompare(b.startDate))[0] ?? t.dateSlots[0];
    if (next) {
      const days = Math.round((Date.parse(next.endDate) - Date.parse(next.startDate)) / 86_400_000) + 1;
      const nights = Math.max(0, days - 1);
      facts.push({ icon: 'clock', label: 'Duration', value: `${days} Day${days === 1 ? '' : 's'}${nights ? ` · ${nights} Night${nights === 1 ? '' : 's'}` : ''}` });
      const d = new Date(next.startDate + 'T00:00:00');
      facts.push({ icon: 'calendar', label: 'Next departure', value: d.toLocaleDateString('en-IN', { day: 'numeric', month: 'short', weekday: 'short' }).replace('Sept', 'Sep') });
    }
    if (t.pickupPoints.length > 0) {
      const extra = t.pickupPoints.length - 1;
      facts.push({ icon: 'pin', label: 'Pickup from', value: t.pickupPoints[0].location + (extra > 0 ? ` +${extra} more` : '') });
    }
    facts.push({ icon: 'tag', label: 'Starting from', value: `₹${t.amountPerPerson.toLocaleString('en-IN')} / person` });
    return facts;
  });

  // ---------- Photo rows ("What you'll see", "Where you'll stay", "How you'll travel") ----------
  // Arrows on desktop (same behaviour as the home page rows); swipe on mobile.

  private readonly hlScroll = viewChild<ElementRef<HTMLElement>>('hlScroll');
  private readonly stayScroll = viewChild<ElementRef<HTMLElement>>('stayScroll');
  private readonly travelScroll = viewChild<ElementRef<HTMLElement>>('travelScroll');
  readonly rowNav = signal<Record<PhotoRow, { prev: boolean; next: boolean }>>({
    hl: { prev: false, next: false },
    stay: { prev: false, next: false },
    travel: { prev: false, next: false },
  });
  private readonly rowNavSync = effect(() => {
    // Re-check whenever any row renders.
    this.hlScroll();
    this.stayScroll();
    this.travelScroll();
    setTimeout(() => this.updateAllRows());
  });

  private rowEl(row: PhotoRow): HTMLElement | undefined {
    const ref = row === 'hl' ? this.hlScroll() : row === 'stay' ? this.stayScroll() : this.travelScroll();
    return ref?.nativeElement;
  }

  updateRow(row: PhotoRow): void {
    const el = this.rowEl(row);
    if (!el) return;
    const state = { prev: el.scrollLeft > 2, next: el.scrollLeft + el.clientWidth < el.scrollWidth - 2 };
    const current = this.rowNav()[row];
    if (current.prev !== state.prev || current.next !== state.next) {
      this.rowNav.set({ ...this.rowNav(), [row]: state });
    }
  }

  private updateAllRows(): void {
    (['hl', 'stay', 'travel'] as const).forEach((r) => this.updateRow(r));
    this.updateDayRail();
  }

  /** Moves a row by one card. */
  scrollRow(row: PhotoRow, direction: 1 | -1): void {
    const el = this.rowEl(row);
    const card = el?.querySelector<HTMLElement>('.hlcard');
    if (!el || !card) return;
    const gap = parseFloat(getComputedStyle(el).columnGap) || 0;
    scrollRowBy(el, direction * (card.getBoundingClientRect().width + gap), '.hlcard');
  }

  // ---------- Photo viewer for "Where you'll stay" / "How you'll travel" ----------

  readonly photoViewer = signal<{ title: string; urls: string[]; index: number } | null>(null);

  openPhotos(title: string, urls: string[], index: number): void {
    this.photoViewer.set({ title, urls, index });
  }

  closePhotos(): void {
    this.photoViewer.set(null);
  }

  stepPhoto(direction: 1 | -1): void {
    const v = this.photoViewer();
    if (!v) return;
    this.photoViewer.set({ ...v, index: (v.index + direction + v.urls.length) % v.urls.length });
  }

  @HostListener('document:keydown', ['$event'])
  onViewerKeydown(event: KeyboardEvent): void {
    if (this.showPickups() && event.key === 'Escape') {
      this.showPickups.set(false);
      return;
    }
    if (this.photoViewer()) {
      if (event.key === 'Escape') this.closePhotos();
      else if (event.key === 'ArrowRight') this.stepPhoto(1);
      else if (event.key === 'ArrowLeft') this.stepPhoto(-1);
    } else if (this.selectedHighlight()) {
      if (event.key === 'Escape') this.closeHighlight();
      else if (event.key === 'ArrowRight') this.stepHighlight(1);
      else if (event.key === 'ArrowLeft') this.stepHighlight(-1);
    }
  }

  @HostListener('window:resize')
  onResize(): void {
    this.updateAllRows();
  }

  tripPhotoUrls(t: TripDetail): string[] {
    return t.photos.map((p) => p.imageUrl);
  }

  roomUrls(t: TripDetail): string[] {
    return t.roomPhotos.map((p) => p.imageUrl);
  }

  vehicleUrls(t: TripDetail): string[] {
    return t.vehiclePhotos.map((p) => p.imageUrl);
  }

  closeHighlight(): void {
    this.selectedHighlight.set(null);
  }

  // Opens the booking popup on "Confirm your date". Once a booking request exists the popup goes
  // straight back to its trip summary / payment step instead.
  openBookingModal(): void {
    this.submitError.set(null);
    this.showBookingModal.set(true);

    if (this.bookingResult()) {
      this.bookingStep.set('flow');
      return;
    }

    this.signInStepShown.set(!this.auth.isAuthenticated());
    // Open the date step on the month of the currently selected departure.
    const slot = this.selectedSlot();
    if (slot) this.selectedMonth.set(slot.startDate.slice(0, 7));
    this.bookingStep.set('date');
  }

  confirmDate(): void {
    if (!this.selectedSlot()) return;
    this.bookingStep.set('seats');
  }

  confirmSeats(): void {
    this.bookingStep.set('flow');
    this.tryProceed();
  }

  // "Edit details" on the trip summary: set the unpaid request aside and walk the popup again from
  // the date step. It is reused if nothing changes, or withdrawn when a new request is made.
  editBooking(): void {
    const booking = this.bookingResult()?.booking;
    if (!booking) return;
    this.setAsideBooking();
    this.resetPickup();
    this.submitError.set(null);
    this.selectedMonth.set(booking.startDate.slice(0, 7));
    this.bookingStep.set('date');
  }

  backToDate(): void {
    this.bookingStep.set('date');
  }

  // Keeps the chosen pickup point highlighted, so coming forward again only needs "Continue".
  backToSeats(): void {
    this.pickupConfirmed.set(false);
    this.bookingStep.set('seats');
  }

  // Closing resets the trip page (sidebar back to the date picker + Continue). A paid or "pay
  // later" booking is kept as is; an unpaid one in progress is set aside as a draft.
  closeBookingModal(): void {
    this.showBookingModal.set(false);
    this.showAuthModal.set(false);
    this.showNamePrompt.set(false);
    this.setAsideBooking();
    // Reopening starts fresh: 1 seat, pickup to choose again (a matching draft is still reused).
    if (!this.bookingResult()) {
      this.resetPickup();
      this.resetTravellers(this.selectedSlot());
    }
    this.bookingStep.set('date');
  }

  private setAsideBooking(): void {
    const result = this.bookingResult();
    if (!result || this.paymentConfirmed()) return;
    this.draft = { result, ...this.lastRequest! };
    this.bookingResult.set(null);
  }

  // Walks the popup forward one step at a time: pickup point (if this trip needs one) → sign in
  // → name on file → create the booking request. Each step's own handler calls this again once
  // it's satisfied, so the popup always lands on whatever step is still outstanding.
  private tryProceed(): void {
    if (this.needsPickupSelection()) {
      return;
    }

    if (!this.auth.isAuthenticated()) {
      this.showAuthModal.set(true);
      return;
    }

    // A signed-in customer who signed up with just a phone number has no name on file yet —
    // ask for it now, before their first booking, rather than gating sign-in on it earlier.
    if (!this.auth.currentCustomer()?.name) {
      this.namePromptValue.set('');
      this.namePromptError.set(null);
      this.showNamePrompt.set(true);
      return;
    }

    if (this.bookingResult()) {
      return; // Already created — the popup's payment/result step takes over from here.
    }

    this.proceedToBooking();
  }

  private proceedToBooking(): void {
    const params = {
      slotId: this.selectedSlot()!.tripDateSlotId,
      seats: this.seats(),
      gents: this.gents(),
      pickupId: this.selectedPickupPointId(),
    };

    // Same date, seats and pickup as the request set aside earlier → reuse it as is.
    const draft = this.draft;
    if (draft && draft.slotId === params.slotId && draft.seats === params.seats && draft.gents === params.gents && draft.pickupId === params.pickupId) {
      this.draft = null;
      this.lastRequest = params;
      this.bookingResult.set(draft.result);
      return;
    }

    this.submitting.set(true);
    this.submitError.set(null);

    // Details changed → withdraw the old unpaid request first (a status flip: nothing charged, no
    // seats held), so the customer never ends up with two open requests for this trip.
    if (draft) {
      this.draft = null;
      this.bookingService.cancelBooking(draft.result.booking.bookingId).subscribe({
        next: () => this.createRequest(params),
        error: () => this.createRequest(params),
      });
      return;
    }

    this.createRequest(params);
  }

  private createRequest(params: { slotId: number; seats: number; gents: number; pickupId: number | null }): void {
    // One id per booking attempt — reused on any retry (network error, double-click) so the
    // backend can safely no-op a duplicate instead of creating a second booking.
    this.clientRequestId ??= crypto.randomUUID();

    this.bookingService
      .requestBooking({
        tripId: this.tripId,
        tripDateSlotId: params.slotId,
        numberOfSeats: params.seats,
        maleCount: params.gents,
        femaleCount: params.seats - params.gents,
        customerNotes: null,
        clientRequestId: this.clientRequestId,
        pickupPointId: params.pickupId,
        // Accepted by continuing past the Seats step, which shows the "By continuing, you agree…" line.
        agreedToTerms: true,
      })
      .subscribe({
        next: (result) => {
          this.submitting.set(false);
          this.justAuthenticated = false;
          this.clientRequestId = null;
          this.lastRequest = params;
          this.bookingResult.set(result);
          // Closed while the request was being created → keep it as the draft, page stays reset.
          if (!this.showBookingModal()) this.setAsideBooking();
        },
        error: () => {
          this.submitting.set(false);
          if (this.justAuthenticated) {
            // The customer is signed in fine — only the booking step failed. Keep them signed in
            // and let them retry without repeating OTP verification.
            this.submitError.set('Your account has been verified, but we could not create the booking request. Please try again.');
          }
          this.justAuthenticated = false;
        },
      });
  }

  onAuthenticated(_response: CustomerAuthResponse): void {
    this.showAuthModal.set(false);
    this.justAuthenticated = true;
    // Resume the booking the customer was already mid-way through — no second "Book" click needed.
    this.tryProceed();
  }

  onAuthCancelled(): void {
    this.closeBookingModal();
  }

  submitNamePrompt(): void {
    const name = this.namePromptValue().trim();
    if (!name) {
      this.namePromptError.set('Enter your name.');
      return;
    }

    this.namePromptSubmitting.set(true);
    this.namePromptError.set(null);

    this.profileService.updateProfile({ name, email: this.auth.currentCustomer()?.email ?? null }).subscribe({
      next: () => {
        this.auth.restoreSession().subscribe(() => {
          this.namePromptSubmitting.set(false);
          this.showNamePrompt.set(false);
          this.tryProceed();
        });
      },
      error: () => {
        this.namePromptSubmitting.set(false);
        this.namePromptError.set('Could not save your name. Please try again.');
      },
    });
  }

  cancelNamePrompt(): void {
    this.closeBookingModal();
  }

  onPaymentConfirmed(): void {
    const bookingId = this.bookingResult()?.booking.bookingId;
    if (!bookingId) return;

    // Refetch rather than trust anything computed client-side — this is the server's own
    // record of what was actually charged/confirmed.
    this.bookingService.getMyBooking(bookingId).subscribe({
      next: (booking) => {
        this.confirmedBooking.set(booking);
        this.paymentConfirmed.set(true);
        this.invoiceEmail.set(this.auth.currentCustomer()?.email ?? '');
      },
    });
  }

  // ---------- Invoice by email (shown once the booking is confirmed) ----------

  readonly invoiceEmail = signal('');
  readonly sendingInvoiceEmail = signal(false);
  readonly invoiceEmailError = signal<string | null>(null);
  readonly invoiceEmailSentTo = signal<string | null>(null);

  emailInvoice(): void {
    const bookingId = this.bookingResult()?.booking.bookingId;
    const email = this.invoiceEmail().trim();
    if (!bookingId || this.sendingInvoiceEmail()) return;
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      this.invoiceEmailError.set('Enter a valid email ID.');
      return;
    }

    this.sendingInvoiceEmail.set(true);
    this.invoiceEmailError.set(null);
    this.bookingService.emailInvoice(bookingId, email).subscribe({
      next: () => {
        this.sendingInvoiceEmail.set(false);
        this.invoiceEmailSentTo.set(email);
      },
      error: (err) => {
        this.sendingInvoiceEmail.set(false);
        this.invoiceEmailError.set(
          err?.status === 429
            ? 'Too many requests. Please try again in a few minutes.'
            : err?.error?.errors?.[0] || err?.error?.message || 'Could not send the invoice. Please try again.',
        );
      },
    });
  }

  downloadInvoice(): void {
    const booking = this.bookingResult()?.booking;
    const bookingId = booking?.bookingId;
    if (!bookingId || this.downloadingInvoice()) return;

    this.downloadingInvoice.set(true);
    this.bookingService.downloadInvoice(bookingId).subscribe({
      next: (blob) => {
        downloadFile(blob, `GhumoOdisha-Invoice-${booking.bookingReference}.pdf`);
        this.downloadingInvoice.set(false);
      },
      error: () => this.downloadingInvoice.set(false),
    });
  }

  onCouponChange(selection: CouponSelection | null): void {
    this.coupon.set(selection);
  }
}
