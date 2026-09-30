import { CommonModule, formatDate } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CustomerBookingService } from '../../core/services/customer-booking.service';
import { BookingResponse } from '../../core/models/booking.model';
import { BookingStatus, BookingStatusLabels, PaymentMethod, PaymentMethodLabels, PaymentStatus, PaymentStatusLabels, RefundStatus, bookingStatusBadgeClass } from '../../core/models/enums.model';
import { BookingTimelineComponent } from '../../shared/components/booking-timeline.component';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { downloadFile } from '../../shared/utils/download-file';
import { MyCarBookingsComponent } from '../cars/my-car-bookings.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-my-bookings',
  standalone: true,
  imports: [CommonModule, RouterLink, StatePanelComponent, ImageUrlPipe, BookingTimelineComponent, MyCarBookingsComponent],
  templateUrl: './my-bookings.component.html',
})
export class MyBookingsComponent implements OnInit {
  private readonly bookingService = inject(CustomerBookingService);

  readonly state = signal<LoadState>('loading');
  readonly bookings = signal<BookingResponse[]>([]);
  // Always opens on Confirmed: the upcoming trips people come here to check.
  readonly filter = signal<BookingStatus | null>(BookingStatus.Confirmed);
  readonly selected = signal<BookingResponse | null>(null);
  readonly downloadingInvoiceId = signal<number | null>(null);
  readonly confirmingCancelId = signal<number | null>(null);
  readonly cancellingId = signal<number | null>(null);
  readonly cancelError = signal<string | null>(null);

  readonly BookingStatus = BookingStatus;
  readonly PaymentStatus = PaymentStatus;
  readonly BookingStatusLabels = BookingStatusLabels;
  readonly PaymentStatusLabels = PaymentStatusLabels;
  readonly PaymentMethodLabels = PaymentMethodLabels;
  readonly bookingStatusBadgeClass = bookingStatusBadgeClass;

  readonly filtered = computed(() => {
    const f = this.filter();
    return f === null ? this.bookings() : this.bookings().filter((b) => b.bookingStatus === f);
  });

  readonly filters: { label: string; value: BookingStatus | null }[] = [
    { label: 'Confirmed', value: BookingStatus.Confirmed },
    { label: 'Completed', value: BookingStatus.Completed },
    { label: 'Cancelled', value: BookingStatus.Cancelled },
    { label: 'All', value: null },
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
    // Linked travellers (added by the organizer) can view the booking but not its invoice.
    return booking.isOwner && (booking.bookingStatus === BookingStatus.Confirmed || booking.bookingStatus === BookingStatus.Completed);
  }

  downloadInvoice(booking: BookingResponse): void {
    if (this.downloadingInvoiceId()) return;
    this.downloadingInvoiceId.set(booking.bookingId);
    this.bookingService.downloadInvoice(booking.bookingId).subscribe({
      next: (blob) => {
        downloadFile(blob, `GhumoOdisha-Invoice-${booking.bookingReference}.pdf`);
        this.downloadingInvoiceId.set(null);
      },
      error: () => this.downloadingInvoiceId.set(null),
    });
  }

  canCancel(booking: BookingResponse): boolean {
    return booking.isOwner && booking.bookingStatus === BookingStatus.Confirmed;
  }

  openDetails(booking: BookingResponse): void {
    this.selected.set(booking);
  }

  /**
   * Customer-facing refund wording. Refunds are issued by our team (never instantly on cancel),
   * so this reads as a calm progress update rather than a raw status.
   */
  refundInfo(booking: BookingResponse): { title: string; detail: string; tone: 'wait' | 'info' | 'ok' } | null {
    const r = booking.refund;
    if (!r) return null;
    const amount = `₹${r.amount.toLocaleString('en-IN', { maximumFractionDigits: 2 })}`;
    const date = (iso: string) => formatDate(iso, 'd MMM y', 'en-US');
    const via = r.method === null ? '' : r.method === PaymentMethod.Razorpay ? 'to your original payment method' : `by ${PaymentMethodLabels[r.method]}`;
    const ref = r.reference ? ` · Ref ${r.reference}` : '';

    switch (r.status) {
      case RefundStatus.Pending:
        return {
          title: `Refund of ${amount} requested`,
          detail: 'Your refund is being processed by our team. We\'ll let you know as soon as it\'s on its way.',
          tone: 'wait',
        };
      case RefundStatus.Processing:
        return {
          title: `Refund of ${amount} initiated`,
          detail: r.method === PaymentMethod.Razorpay
            ? `Sent ${via} on ${date(r.initiatedAt!)}. It usually reaches your account within 5–7 working days${ref}.`
            : `Being transferred ${via} (initiated ${date(r.initiatedAt!)})${ref}.`,
          tone: 'info',
        };
      case RefundStatus.Settled:
        return {
          title: `${amount} refunded`,
          detail: `Completed on ${date(r.settledAt!)}${via ? ' ' + via : ''}${ref}.`,
          tone: 'ok',
        };
    }
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
