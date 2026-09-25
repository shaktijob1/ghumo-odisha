import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, computed, inject, signal, viewChild, viewChildren } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PublicTripService } from '../../core/services/public-trip.service';
import { CustomerBookingService } from '../../core/services/customer-booking.service';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { CustomerProfileService } from '../../core/services/customer-profile.service';
import { ContactService } from '../../core/services/contact.service';
import { ToastService } from '../../core/services/toast.service';
import { TripDetail, DateSlot, TripHighlight } from '../../core/models/trip.model';
import { BookingResponse, CreateBookingResult } from '../../core/models/booking.model';
import { CustomerAuthResponse } from '../../core/models/auth.model';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { SeatSelectorComponent } from '../../shared/components/seat-selector.component';
import { WhatsappAuthComponent } from '../../shared/components/whatsapp-auth.component';
import { PaymentPanelComponent } from '../../shared/components/payment-panel.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { downloadFile } from '../../shared/utils/download-file';
import { toLocalDateKey } from '../../shared/utils/date-key';
import { BookingPriceSummaryComponent, payNowFor } from '../../shared/components/booking-price-summary.component';
import { CouponFieldComponent, CouponSelection } from '../../shared/components/coupon-field.component';

type LoadState = 'loading' | 'ready' | 'error';


const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

@Component({
  selector: 'app-trip-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent, SeatSelectorComponent, WhatsappAuthComponent, PaymentPanelComponent, ImageUrlPipe, BookingPriceSummaryComponent, CouponFieldComponent],
  templateUrl: './trip-detail.component.html',
})
export class TripDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly tripService = inject(PublicTripService);
  private readonly bookingService = inject(CustomerBookingService);
  readonly auth = inject(CustomerAuthService);
  private readonly contactService = inject(ContactService);
  private readonly profileService = inject(CustomerProfileService);
  private readonly toast = inject(ToastService);

  readonly contact = this.contactService.get();
  readonly state = signal<LoadState>('loading');
  readonly trip = signal<TripDetail | null>(null);

  readonly heroIndex = signal(0);
  readonly selectedSlotId = signal<number | null>(null);
  readonly seats = signal(1);
  // Chosen in the sidebar; carried into the payment step, where it is (re)validated for this customer.
  readonly coupon = signal<CouponSelection | null>(null);
  readonly couponDiscount = computed(() => (this.coupon()?.validated ? this.coupon()!.discountAmount : 0));
  readonly termsAccepted = signal(false);
  readonly submitting = signal(false);
  readonly submitError = signal<string | null>(null);
  readonly bookingResult = signal<CreateBookingResult | null>(null);
  readonly showAuthModal = signal(false);

  readonly paymentConfirmed = signal(false);
  readonly confirmedBooking = signal<BookingResponse | null>(null);
  readonly skippedPayment = signal(false);
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

  // Day tabs auto-advance on small screens only, where the connector arrows are hidden and the
  // tab strip behaves like a carousel instead — desktop keeps the arrows and manual clicking.
  private readonly dayTabEls = viewChildren<ElementRef<HTMLButtonElement>>('dayTab');
  private readonly dayTabsEl = viewChild<ElementRef<HTMLDivElement>>('dayTabsEl');
  private readonly mobileMql = typeof window !== 'undefined' ? window.matchMedia('(max-width: 640px)') : null;
  private dayTimer?: ReturnType<typeof setInterval>;
  private readonly onMobileMqlChange = (e: MediaQueryListEvent): void => {
    if (e.matches) this.startDayAutoplay();
    else this.stopDayAutoplay();
  };

  tripId!: number;
  private clientRequestId: string | null = null;
  private justAuthenticated = false;

  readonly selectedSlot = computed<DateSlot | null>(() => {
    const slots = this.trip()?.dateSlots ?? [];
    return slots.find((s) => s.tripDateSlotId === this.selectedSlotId()) ?? null;
  });

  readonly includedItems = computed(() => {
    const inc = this.trip()?.inclusions;
    if (!inc) return [];
    const items: { key: 'breakfast' | 'lunch' | 'dinner' | 'stay' | 'coordinator'; label: string; caption: string }[] = [];
    if (inc.breakfast) items.push({ key: 'breakfast', label: 'Breakfast included', caption: 'Breakfast' });
    if (inc.lunch) items.push({ key: 'lunch', label: 'Lunch included', caption: 'Lunch' });
    if (inc.dinner) items.push({ key: 'dinner', label: 'Dinner included', caption: 'Dinner' });
    if (inc.stay) items.push({ key: 'stay', label: 'Stay included', caption: 'Stay' });
    if (inc.coordinator) items.push({ key: 'coordinator', label: 'Trip coordinator included', caption: 'Coordinator' });
    return items;
  });

  readonly totalAmount = computed(() => (this.trip()?.amountPerPerson ?? 0) * this.seats());

  // What's actually collected via Razorpay to confirm the booking — ₹99/seat, same rate the
  // payment panel charges once the booking request exists. Never used to compute what's actually
  // charged; that's recomputed server-side from scratch.
  readonly bookingAdvance = computed(() => payNowFor(this.trip()?.amountPerPerson ?? 0, this.seats(), this.couponDiscount()));

  readonly requiresPickupPoint = computed(() => (this.trip()?.pickupPoints.length ?? 0) > 0);

  readonly showBookingModal = signal(false);
  readonly needsPickupSelection = computed(
    () => this.requiresPickupPoint() && !this.selectedPickupPointId() && !this.bookingResult(),
  );

  ngOnInit(): void {
    this.tripId = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
    this.mobileMql?.addEventListener('change', this.onMobileMqlChange);
  }

  ngOnDestroy(): void {
    this.stopHeroAutoplay();
    this.stopDayAutoplay();
    this.mobileMql?.removeEventListener('change', this.onMobileMqlChange);
  }

  load(): void {
    this.state.set('loading');
    this.tripService.getTrip(this.tripId).subscribe({
      next: (t) => {
        this.trip.set(t);
        const firstOpenSlot = this.upcomingSlots().find((s) => !s.isSoldOut);
        this.selectedSlotId.set(firstOpenSlot?.tripDateSlotId ?? null);
        // Open on the month holding the next bookable departure (e.g. October if September is
        // empty or sold out), falling back to the first month that has any departure at all.
        this.selectedMonth.set(firstOpenSlot?.startDate.slice(0, 7) ?? this.slotMonths()[0]?.key ?? null);
        // No pickup point is pre-selected — choosing one is now an explicit step in the booking
        // popup, not a silently-applied default the customer might not notice.
        this.selectedPickupPointId.set(null);
        this.selectedDayIndex.set(0);
        this.showAllSlots.set(false);
        this.state.set('ready');
        this.startHeroAutoplay();
        this.startDayAutoplay();
      },
      error: () => this.state.set('error'),
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

  private startDayAutoplay(): void {
    this.stopDayAutoplay();
    if (!this.mobileMql?.matches || (this.trip()?.itineraryDays.length ?? 0) <= 1) {
      return;
    }
    this.dayTimer = setInterval(() => this.advanceDay(), 10000);
  }

  private stopDayAutoplay(): void {
    if (this.dayTimer) {
      clearInterval(this.dayTimer);
      this.dayTimer = undefined;
    }
  }

  private advanceDay(): void {
    const total = this.trip()?.itineraryDays.length ?? 0;
    if (total === 0) return;
    this.selectDay((this.selectedDayIndex() + 1) % total);
  }

  // Used by both the manual tap and the mobile autoplay tick — a manual tap also scrolls the tab
  // into view and restarts the timer so it doesn't immediately override what the customer picked.
  selectDay(index: number): void {
    this.selectedDayIndex.set(index);

    // Scrolls only the tab strip itself (never scrollIntoView, which also drags the whole page's
    // vertical scroll toward this section — jarring when this fires on its own every 10s).
    const container = this.dayTabsEl()?.nativeElement;
    const btn = this.dayTabEls()[index]?.nativeElement;
    if (container && btn) {
      const target = btn.offsetLeft - (container.clientWidth - btn.clientWidth) / 2;
      container.scrollTo({ left: target, behavior: 'smooth' });
    }

    this.startDayAutoplay();
  }

  selectPickupPoint(id: number): void {
    this.selectedPickupPointId.set(id);
    this.tryProceed();
  }

  selectMonth(key: string): void {
    this.selectedMonth.set(key);
    this.showAllSlots.set(false);
  }

  selectSlot(slot: DateSlot): void {
    if (slot.isSoldOut) return;
    this.selectedSlotId.set(slot.tripDateSlotId);
    this.seats.set(1);
  }

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

  closeHighlight(): void {
    this.selectedHighlight.set(null);
  }

  // Opens the booking popup — pickup point, sign-in and payment all happen as steps inside it.
  openBookingModal(): void {
    if (!this.selectedSlot()) {
      this.submitError.set('Choose a date first.');
      return;
    }

    this.submitError.set(null);
    this.showBookingModal.set(true);
    this.tryProceed();
  }

  closeBookingModal(): void {
    this.showBookingModal.set(false);
    this.showAuthModal.set(false);
    this.showNamePrompt.set(false);
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
    // One id per booking attempt — reused on any retry (network error, double-click) so the
    // backend can safely no-op a duplicate instead of creating a second booking.
    this.clientRequestId ??= crypto.randomUUID();

    this.submitting.set(true);
    this.submitError.set(null);

    this.bookingService
      .requestBooking({
        tripId: this.tripId,
        tripDateSlotId: this.selectedSlot()!.tripDateSlotId,
        numberOfSeats: this.seats(),
        customerNotes: null,
        clientRequestId: this.clientRequestId,
        pickupPointId: this.selectedPickupPointId(),
        agreedToTerms: this.termsAccepted(),
      })
      .subscribe({
        next: (result) => {
          this.submitting.set(false);
          this.justAuthenticated = false;
          this.bookingResult.set(result);
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
      },
    });
  }

  downloadInvoice(): void {
    const bookingId = this.bookingResult()?.booking.bookingId;
    if (!bookingId || this.downloadingInvoice()) return;

    this.downloadingInvoice.set(true);
    this.bookingService.downloadInvoice(bookingId).subscribe({
      next: (blob) => {
        downloadFile(blob, `GhumoOdisha-Invoice-GO-${bookingId}.pdf`);
        this.downloadingInvoice.set(false);
      },
      error: () => this.downloadingInvoice.set(false),
    });
  }

  onCouponChange(selection: CouponSelection | null): void {
    this.coupon.set(selection);
  }
}
