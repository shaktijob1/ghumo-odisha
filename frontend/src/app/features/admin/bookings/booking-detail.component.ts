import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminBookingService } from '../../../core/services/admin-booking.service';
import { AdminBookingDetail } from '../../../core/models/booking.model';
import { BookingStatus } from '../../../core/models/enums.model';
import { ApiResponse } from '../../../core/models/api-response.model';
import { ToastService } from '../../../core/services/toast.service';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog.component';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-booking-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent, StatusBadgeComponent, ConfirmDialogComponent],
  templateUrl: './booking-detail.component.html',
})
export class BookingDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly bookingService = inject(AdminBookingService);
  private readonly toast = inject(ToastService);

  readonly BookingStatus = BookingStatus;
  readonly state = signal<LoadState>('loading');
  readonly booking = signal<AdminBookingDetail | null>(null);

  bookingId!: number;

  readonly showConfirmModal = signal(false);
  readonly showRejectModal = signal(false);
  readonly showCancelModal = signal(false);
  readonly actionError = signal<string | null>(null);
  readonly submitting = signal(false);

  advanceAmount = 0;
  adminNotes = '';

  ngOnInit(): void {
    this.bookingId = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.bookingService.getBooking(this.bookingId).subscribe({
      next: (b) => {
        this.booking.set(b);
        this.advanceAmount = b.advanceAmount;
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  openConfirmModal(): void {
    this.actionError.set(null);
    this.advanceAmount = this.booking()?.advanceAmount ?? 0;
    this.showConfirmModal.set(true);
  }

  submitConfirm(): void {
    this.submitting.set(true);
    this.actionError.set(null);
    this.bookingService.confirmBooking(this.bookingId, { advanceAmount: this.advanceAmount }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.showConfirmModal.set(false);
        this.toast.success('Booking confirmed.');
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        const body = err.error as ApiResponse<unknown> | undefined;
        this.actionError.set(body?.message ?? 'Could not confirm this booking.');
      },
    });
  }

  submitReject(): void {
    this.submitting.set(true);
    this.bookingService.rejectBooking(this.bookingId, { adminNotes: this.adminNotes || null }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.showRejectModal.set(false);
        this.toast.success('Booking rejected.');
        this.load();
      },
      error: () => this.submitting.set(false),
    });
  }

  submitCancel(): void {
    this.submitting.set(true);
    this.bookingService.cancelBooking(this.bookingId, { adminNotes: this.adminNotes || null }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.showCancelModal.set(false);
        this.toast.success('Booking cancelled.');
        this.load();
      },
      error: () => this.submitting.set(false),
    });
  }

  get remaining(): number {
    const b = this.booking();
    if (!b) return 0;
    return Math.max(0, b.totalAmount - this.advanceAmount);
  }
}
