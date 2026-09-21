import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CustomerBookingService } from '../../core/services/customer-booking.service';
import { BookingResponse } from '../../core/models/booking.model';
import { BookingStatus, BookingStatusLabels, PaymentStatusLabels, bookingStatusBadgeClass } from '../../core/models/enums.model';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { PaymentPanelComponent } from '../../shared/components/payment-panel.component';
import { downloadFile } from '../../shared/utils/download-file';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-my-bookings',
  standalone: true,
  imports: [CommonModule, RouterLink, StatePanelComponent, PaymentPanelComponent, ImageUrlPipe],
  templateUrl: './my-bookings.component.html',
})
export class MyBookingsComponent implements OnInit {
  private readonly bookingService = inject(CustomerBookingService);

  readonly state = signal<LoadState>('loading');
  readonly bookings = signal<BookingResponse[]>([]);
  readonly filter = signal<BookingStatus | null>(null);
  readonly selected = signal<BookingResponse | null>(null);
  readonly downloadingInvoiceId = signal<number | null>(null);
  readonly confirmingCancelId = signal<number | null>(null);
  readonly cancellingId = signal<number | null>(null);
  readonly cancelError = signal<string | null>(null);

  readonly BookingStatus = BookingStatus;
  readonly BookingStatusLabels = BookingStatusLabels;
  readonly PaymentStatusLabels = PaymentStatusLabels;
  readonly bookingStatusBadgeClass = bookingStatusBadgeClass;

  readonly filtered = computed(() => {
    const f = this.filter();
    return f === null ? this.bookings() : this.bookings().filter((b) => b.bookingStatus === f);
  });

  readonly filters: { label: string; value: BookingStatus | null }[] = [
    { label: 'All', value: null },
    { label: 'Requested', value: BookingStatus.Requested },
    { label: 'Confirmed', value: BookingStatus.Confirmed },
    { label: 'Cancelled', value: BookingStatus.Cancelled },
    { label: 'Completed', value: BookingStatus.Completed },
  ];

  ngOnInit(): void {
    this.bookingService.getMyBookings(1, 50).subscribe({
      next: (r) => {
        this.bookings.set(r.items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  canDownloadInvoice(booking: BookingResponse): boolean {
    return booking.bookingStatus === BookingStatus.Confirmed || booking.bookingStatus === BookingStatus.Completed;
  }

  downloadInvoice(booking: BookingResponse): void {
    if (this.downloadingInvoiceId()) return;
    this.downloadingInvoiceId.set(booking.bookingId);
    this.bookingService.downloadInvoice(booking.bookingId).subscribe({
      next: (blob) => {
        downloadFile(blob, `GhumoOdisha-Invoice-GO-${booking.bookingId}.pdf`);
        this.downloadingInvoiceId.set(null);
      },
      error: () => this.downloadingInvoiceId.set(null),
    });
  }

  canCancel(booking: BookingResponse): boolean {
    return booking.bookingStatus === BookingStatus.Confirmed || booking.bookingStatus === BookingStatus.Requested;
  }

  canPayNow(booking: BookingResponse): boolean {
    return booking.bookingStatus === BookingStatus.Requested;
  }

  onPaymentConfirmed(booking: BookingResponse): void {
    // Refetch so the card/modal show the real server-confirmed amounts and status.
    this.bookingService.getMyBooking(booking.bookingId).subscribe({
      next: (updated) => {
        this.bookings.update((list) => list.map((b) => (b.bookingId === updated.bookingId ? updated : b)));
        if (this.selected()?.bookingId === updated.bookingId) {
          this.selected.set(updated);
        }
      },
    });
  }

  requestCancelConfirmation(booking: BookingResponse): void {
    this.cancelError.set(null);
    this.confirmingCancelId.set(booking.bookingId);
  }

  cancelCancelConfirmation(): void {
    this.confirmingCancelId.set(null);
  }

  confirmCancel(booking: BookingResponse): void {
    if (this.cancellingId()) return;
    this.cancellingId.set(booking.bookingId);
    this.cancelError.set(null);

    this.bookingService.cancelBooking(booking.bookingId).subscribe({
      next: (updated) => {
        this.cancellingId.set(null);
        this.confirmingCancelId.set(null);
        this.bookings.update((list) => list.map((b) => (b.bookingId === updated.bookingId ? updated : b)));
        if (this.selected()?.bookingId === updated.bookingId) {
          this.selected.set(updated);
        }
      },
      error: (err) => {
        this.cancellingId.set(null);
        this.cancelError.set(err?.error?.message || 'Could not cancel this booking. Please try again.');
      },
    });
  }
}
