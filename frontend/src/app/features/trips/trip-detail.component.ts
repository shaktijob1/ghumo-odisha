import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PublicTripService } from '../../core/services/public-trip.service';
import { CustomerBookingService } from '../../core/services/customer-booking.service';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { CustomerProfileService } from '../../core/services/customer-profile.service';
import { ContactService } from '../../core/services/contact.service';
import { TripDetail, DateSlot } from '../../core/models/trip.model';
import { BookingResponse, CreateBookingResult } from '../../core/models/booking.model';
import { CustomerAuthResponse } from '../../core/models/auth.model';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { SeatSelectorComponent } from '../../shared/components/seat-selector.component';
import { WhatsappAuthComponent } from '../../shared/components/whatsapp-auth.component';
import { PaymentPanelComponent } from '../../shared/components/payment-panel.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { downloadFile } from '../../shared/utils/download-file';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-trip-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent, SeatSelectorComponent, WhatsappAuthComponent, PaymentPanelComponent, ImageUrlPipe],
  templateUrl: './trip-detail.component.html',
})
export class TripDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly tripService = inject(PublicTripService);
  private readonly bookingService = inject(CustomerBookingService);
  readonly auth = inject(CustomerAuthService);
  private readonly contactService = inject(ContactService);
  private readonly profileService = inject(CustomerProfileService);

  readonly contact = this.contactService.get();
  readonly state = signal<LoadState>('loading');
  readonly trip = signal<TripDetail | null>(null);

  readonly heroIndex = signal(0);
  readonly selectedSlotId = signal<number | null>(null);
  readonly seats = signal(1);
  readonly customerNotes = signal('');
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
    const items: string[] = [];
    if (inc.breakfast) items.push('Breakfast');
    if (inc.lunch) items.push('Lunch');
    if (inc.dinner) items.push('Dinner');
    if (inc.stay) items.push('Stay');
    if (inc.coordinator) items.push('Trip coordinator');
    return items;
  });

  readonly totalAmount = computed(() => (this.trip()?.amountPerPerson ?? 0) * this.seats());

  readonly requiresPickupPoint = computed(() => (this.trip()?.pickupPoints.length ?? 0) > 0);

  ngOnInit(): void {
    this.tripId = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.tripService.getTrip(this.tripId).subscribe({
      next: (t) => {
        this.trip.set(t);
        const firstOpenSlot = t.dateSlots.find((s) => !s.isSoldOut);
        this.selectedSlotId.set(firstOpenSlot?.tripDateSlotId ?? null);
        this.selectedPickupPointId.set(t.pickupPoints[0]?.pickupPointId ?? null);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  selectPickupPoint(id: number): void {
    this.selectedPickupPointId.set(id);
  }

  selectSlot(slot: DateSlot): void {
    if (slot.isSoldOut) return;
    this.selectedSlotId.set(slot.tripDateSlotId);
    this.seats.set(1);
  }

  nextHero(): void {
    const total = this.trip()?.photos.length ?? 0;
    if (total === 0) return;
    this.heroIndex.set((this.heroIndex() + 1) % total);
  }

  prevHero(): void {
    const total = this.trip()?.photos.length ?? 0;
    if (total === 0) return;
    this.heroIndex.set((this.heroIndex() - 1 + total) % total);
  }

  submitBooking(): void {
    if (!this.selectedSlot()) {
      this.submitError.set('Choose a date first.');
      return;
    }

    if (this.requiresPickupPoint() && !this.selectedPickupPointId()) {
      this.submitError.set('Choose a pickup point first.');
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
        customerNotes: this.customerNotes() || null,
        clientRequestId: this.clientRequestId,
        pickupPointId: this.selectedPickupPointId(),
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
    this.submitBooking();
  }

  onAuthCancelled(): void {
    this.showAuthModal.set(false);
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
          this.proceedToBooking();
        });
      },
      error: () => {
        this.namePromptSubmitting.set(false);
        this.namePromptError.set('Could not save your name. Please try again.');
      },
    });
  }

  cancelNamePrompt(): void {
    this.showNamePrompt.set(false);
  }

  openWhatsApp(): void {
    const message = this.bookingResult()?.whatsAppMessage;
    if (!message) return;
    window.open(this.contactService.buildWhatsAppLink(message), '_blank');
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
}
