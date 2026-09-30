import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiResponse } from '../../../core/models/api-response.model';
import { CollectionBooking, CollectionSheet, CollectionTrip, PaymentQr } from '../../../core/models/collection.model';
import { OfflinePaymentMethods, PaymentMethod, PaymentMethodLabels, PaymentStatus, PaymentStatusLabels } from '../../../core/models/enums.model';
import { AdminBookingService } from '../../../core/services/admin-booking.service';
import { AdminCollectionService } from '../../../core/services/admin-collection.service';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog.component';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';
import { toLocalDateKey } from '../../../shared/utils/date-key';

type LoadState = 'loading' | 'ready' | 'error';
type Show = 'all' | 'due' | 'paid';

/**
 * Collections desk for trip coordinators: pick a trip (and departure) to see every confirmed
 * booking with what's been paid and what's still due. "Mark paid" records the amount collected
 * through the normal booking payment ledger; the payment QR opens full-screen for the traveller to scan.
 */
@Component({
  selector: 'app-collections',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent, ImageUrlPipe, ConfirmDialogComponent],
  templateUrl: './collections.component.html',
  styleUrls: ['./collections.component.css', './collections.qr.css'],
})
export class CollectionsComponent implements OnInit {
  private readonly collections = inject(AdminCollectionService);
  private readonly bookings = inject(AdminBookingService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly PaymentStatus = PaymentStatus;
  readonly PaymentStatusLabels = PaymentStatusLabels;
  readonly PaymentMethodLabels = PaymentMethodLabels;
  readonly offlineMethods = OfflinePaymentMethods;

  readonly tripsState = signal<LoadState>('loading');
  readonly trips = signal<CollectionTrip[]>([]);
  readonly tripId = signal<number | null>(null);
  readonly slotId = signal<number | null>(null);

  readonly sheetState = signal<LoadState>('loading');
  readonly sheet = signal<CollectionSheet | null>(null);
  readonly search = signal('');
  readonly show = signal<Show>('all');

  readonly selectedTrip = computed(() => this.trips().find((t) => t.tripId === this.tripId()) ?? null);

  readonly visibleItems = computed(() => {
    const s = this.sheet();
    if (!s) return [];
    const q = this.search().trim().toLowerCase();
    const show = this.show();
    return s.items.filter((i) => {
      if (show === 'due' && i.remaining <= 0) return false;
      if (show === 'paid' && i.remaining > 0) return false;
      if (!q) return true;
      return (
        i.customerName.toLowerCase().includes(q) ||
        (i.customerPhone ?? '').includes(q) ||
        i.bookingReference.toLowerCase().includes(q)
      );
    });
  });

  readonly dueCount = computed(() => this.sheet()?.items.filter((i) => i.remaining > 0).length ?? 0);
  readonly showDepartureColumn = computed(() => this.slotId() === null && (this.selectedTrip()?.departures.length ?? 0) > 1);

  // ---- mark paid ----
  readonly paying = signal<CollectionBooking | null>(null);
  readonly submitting = signal(false);
  readonly payError = signal<string | null>(null);
  payAmount = 0;
  payMethod: PaymentMethod = PaymentMethod.Cash;
  payReference = '';

  // ---- payment QR ----
  readonly qr = signal<PaymentQr | null>(null);
  readonly qrLoaded = signal(false);
  /** Full-screen QR; `forBooking` adds the traveller's name and amount due above it. */
  readonly qrOpen = signal<{ forBooking: CollectionBooking | null } | null>(null);
  readonly qrEditing = signal(false);
  readonly qrSaving = signal(false);
  readonly qrConfirmRemove = signal(false);
  qrFile: File | null = null;
  qrPreview: string | null = null;
  qrCaption = '';

  ngOnInit(): void {
    this.loadTrips();
    this.collections.getQr().subscribe({
      next: (q) => {
        this.qr.set(q);
        this.qrLoaded.set(true);
      },
      error: () => this.qrLoaded.set(true),
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.qrOpen()) this.qrOpen.set(null);
    else if (this.paying()) this.paying.set(null);
  }

  // ---------- trips / sheet ----------

  loadTrips(): void {
    this.tripsState.set('loading');
    this.collections.getTrips().subscribe({
      next: (trips) => {
        this.trips.set(trips);
        this.tripsState.set('ready');
        if (trips.length === 0) return;

        const params = this.route.snapshot.queryParamMap;
        const wantedTrip = Number(params.get('trip')) || null;
        const trip = trips.find((t) => t.tripId === wantedTrip) ?? trips[0];
        const wantedSlot = params.has('slot') ? Number(params.get('slot')) || null : this.defaultSlotId(trip);
        this.select(trip.tripId, trip.departures.some((d) => d.tripDateSlotId === wantedSlot) ? wantedSlot : null);
      },
      error: () => this.tripsState.set('error'),
    });
  }

  /** The departure running now or next — what a coordinator on the road is collecting for. */
  private defaultSlotId(trip: CollectionTrip): number | null {
    const today = toLocalDateKey(new Date());
    return trip.departures.find((d) => d.endDate >= today)?.tripDateSlotId ?? null;
  }

  onTripChange(tripId: number): void {
    const trip = this.trips().find((t) => t.tripId === tripId);
    if (trip) this.select(trip.tripId, this.defaultSlotId(trip));
  }

  onSlotChange(slotId: number | null): void {
    this.select(this.tripId()!, slotId);
  }

  private select(tripId: number, slotId: number | null): void {
    this.tripId.set(tripId);
    this.slotId.set(slotId);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { trip: tripId, slot: slotId ?? 'all' },
      replaceUrl: true,
    });
    this.loadSheet();
  }

  loadSheet(): void {
    const tripId = this.tripId();
    if (!tripId) return;
    this.sheetState.set('loading');
    this.collections.getSheet(tripId, this.slotId()).subscribe({
      next: (s) => {
        this.sheet.set(s);
        this.sheetState.set('ready');
      },
      error: () => this.sheetState.set('error'),
    });
  }

  /** Refreshes the sheet without the loading flash, plus the dropdown's "₹ due" figures. */
  private refresh(): void {
    const tripId = this.tripId();
    if (!tripId) return;
    this.collections.getSheet(tripId, this.slotId()).subscribe({ next: (s) => this.sheet.set(s), error: () => undefined });
    this.collections.getTrips().subscribe({ next: (t) => this.trips.set(t), error: () => undefined });
  }

  statusClass(item: CollectionBooking): string {
    if (item.remaining <= 0) return 'ok';
    return item.paid > 0 ? 'wait' : 'bad';
  }

  // ---------- mark paid ----------

  openPay(item: CollectionBooking): void {
    this.paying.set(item);
    this.payAmount = item.remaining;
    this.payMethod = PaymentMethod.Cash;
    this.payReference = '';
    this.payError.set(null);
  }

  submitPay(): void {
    const item = this.paying();
    if (!item) return;
    if (!(this.payAmount > 0) || this.payAmount > item.remaining) {
      this.payError.set(`Enter an amount between ₹1 and ₹${item.remaining.toLocaleString('en-IN')}.`);
      return;
    }

    this.submitting.set(true);
    this.payError.set(null);
    this.bookings
      .addPayment(item.bookingId, {
        amount: this.payAmount,
        method: this.payMethod,
        reference: this.payReference.trim() || null,
        notes: 'Collected on trip (Collections)',
        paidAt: new Date().toISOString(),
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.paying.set(null);
          const fully = this.payAmount >= item.remaining;
          this.toast.success(`₹${this.payAmount.toLocaleString('en-IN')} recorded for ${item.customerName}${fully ? ' — fully paid' : ''}.`);
          this.refresh();
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          const body = err.error as ApiResponse<unknown> | undefined;
          this.payError.set(body?.errors?.[0] ?? body?.message ?? 'Could not record this payment.');
        },
      });
  }

  // ---------- payment QR ----------

  showQr(forBooking: CollectionBooking | null = null): void {
    if (!this.qr()) {
      this.toast.error('Upload your payment QR first (bottom of this page).');
      return;
    }
    this.qrOpen.set({ forBooking });
  }

  startQrEdit(): void {
    this.qrEditing.set(true);
    this.qrFile = null;
    this.qrPreview = null;
    this.qrCaption = this.qr()?.caption ?? '';
  }

  cancelQrEdit(): void {
    this.qrEditing.set(false);
    if (this.qrPreview) URL.revokeObjectURL(this.qrPreview);
    this.qrPreview = null;
    this.qrFile = null;
  }

  onQrFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    if (!file) return;
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type)) {
      this.toast.error('Choose a PNG, JPG or WebP image of your QR code.');
      input.value = '';
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      this.toast.error('The image must be 5 MB or smaller.');
      input.value = '';
      return;
    }
    if (this.qrPreview) URL.revokeObjectURL(this.qrPreview);
    this.qrFile = file;
    this.qrPreview = URL.createObjectURL(file);
  }

  saveQr(): void {
    if (!this.qrFile) {
      this.toast.error('Choose the QR code image to upload.');
      return;
    }
    this.qrSaving.set(true);
    this.collections.setQr(this.qrFile, this.qrCaption.trim() || null).subscribe({
      next: (q) => {
        this.qr.set(q);
        this.qrSaving.set(false);
        this.cancelQrEdit();
        this.toast.success('Payment QR saved.');
      },
      error: () => this.qrSaving.set(false),
    });
  }

  removeQr(): void {
    this.qrSaving.set(true);
    this.collections.removeQr().subscribe({
      next: () => {
        this.qr.set(null);
        this.qrSaving.set(false);
        this.qrConfirmRemove.set(false);
        this.toast.success('Payment QR removed.');
      },
      error: () => this.qrSaving.set(false),
    });
  }
}
