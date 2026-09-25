import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminBookingService } from '../../../core/services/admin-booking.service';
import { AdminBookingDetail, BookingPayment, TravellerInput } from '../../../core/models/booking.model';
import {
  BookingStatus,
  Gender,
  GenderLabels,
  OfflinePaymentMethods,
  PaymentMethod,
  PaymentMethodLabels,
} from '../../../core/models/enums.model';
import { roomsForSeats } from '../../../shared/utils/rooms';
import { BookingTimelineComponent } from '../../../shared/components/booking-timeline.component';

/** Editable row — strings for the text inputs, trimmed/converted on save. */
interface TravellerRow {
  seatNumber: number;
  fullName: string;
  gender: Gender | null;
  age: number | null;
  aadhaarLast4: string;
  phoneNumber: string;
}
import { downloadFile } from '../../../shared/utils/download-file';
import { toLocalDateKey } from '../../../shared/utils/date-key';
import { ApiResponse } from '../../../core/models/api-response.model';
import { ToastService } from '../../../core/services/toast.service';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog.component';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-booking-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent, StatusBadgeComponent, ConfirmDialogComponent, BookingTimelineComponent],
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
  confirmMethod: PaymentMethod = PaymentMethod.Cash;
  confirmReference = '';
  adminNotes = '';

  readonly PaymentMethodLabels = PaymentMethodLabels;
  readonly PaymentMethod = PaymentMethod;
  readonly offlineMethods = OfflinePaymentMethods;

  // Record-payment modal
  readonly showPaymentModal = signal(false);
  readonly paymentError = signal<string | null>(null);
  paymentAmount = 0;
  paymentMethod: PaymentMethod = PaymentMethod.Cash;
  paymentReference = '';
  paymentNotes = '';
  paymentDate = '';

  readonly paymentToRemove = signal<BookingPayment | null>(null);
  readonly downloadingInvoice = signal(false);

  readonly GenderLabels = GenderLabels;
  readonly genders = [Gender.Male, Gender.Female, Gender.Other];

  // Cancel modal
  cancelWaiveRefund = false;
  cancelReason = '';
  readonly cancelError = signal<string | null>(null);

  // Change-seats modal
  readonly showSeatsModal = signal(false);
  readonly seatsError = signal<string | null>(null);
  newSeats = 1;
  seatsReason = '';

  // Travellers editor
  readonly editingTravellers = signal(false);
  readonly travellersError = signal<string | null>(null);
  travellerRows: TravellerRow[] = [];

  // Male / female modal
  readonly showGenderModal = signal(false);
  readonly genderError = signal<string | null>(null);
  maleCount: number | null = null;
  femaleCount: number | null = null;

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
    this.confirmMethod = PaymentMethod.Cash;
    this.confirmReference = '';
    this.showConfirmModal.set(true);
  }

  submitConfirm(): void {
    this.submitting.set(true);
    this.actionError.set(null);
    const request = {
      advanceAmount: this.advanceAmount,
      method: this.confirmMethod,
      paymentReference: this.confirmReference.trim() || null,
    };
    this.bookingService.confirmBooking(this.bookingId, request).subscribe({
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

  openCancelModal(): void {
    this.cancelWaiveRefund = false;
    this.cancelReason = '';
    this.cancelError.set(null);
    this.showCancelModal.set(true);
  }

  submitCancel(): void {
    const reason = this.cancelReason.trim();
    if (this.cancelWaiveRefund && !reason) {
      this.cancelError.set('Enter a reason for cancelling without a refund.');
      return;
    }

    this.submitting.set(true);
    this.cancelError.set(null);
    this.bookingService
      .cancelBooking(this.bookingId, { adminNotes: this.adminNotes || null, waiveRefund: this.cancelWaiveRefund, reason: reason || null })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.showCancelModal.set(false);
          this.toast.success(this.cancelWaiveRefund ? 'Booking cancelled without refund.' : 'Booking cancelled.');
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          this.cancelError.set(this.errorMessage(err, 'Could not cancel this booking.'));
        },
      });
  }

  // ---------- Seats ----------

  get canChangeSeats(): boolean {
    const s = this.booking()?.bookingStatus;
    return s === BookingStatus.Requested || s === BookingStatus.Pending || s === BookingStatus.Confirmed;
  }

  openSeatsModal(): void {
    this.newSeats = this.booking()?.numberOfSeats ?? 1;
    this.seatsReason = '';
    this.seatsError.set(null);
    this.showSeatsModal.set(true);
  }

  /** Display-only preview — the server recomputes the real total. */
  get seatsPreview(): { total: number; balance: number; excess: number; rooms: number } {
    const b = this.booking();
    if (!b) return { total: 0, balance: 0, excess: 0, rooms: 0 };
    const seats = Math.max(1, Math.floor(this.newSeats || 1));
    const total = Math.max(0, b.amountPerPerson * seats - b.discountAmount);
    return {
      total,
      balance: Math.max(0, total - b.advanceAmount),
      excess: Math.max(0, b.advanceAmount - total),
      rooms: roomsForSeats(seats),
    };
  }

  submitSeats(): void {
    const b = this.booking();
    if (!b) return;
    if (!Number.isInteger(this.newSeats) || this.newSeats < 1) {
      this.seatsError.set('Enter at least 1 seat — cancel the booking to remove it entirely.');
      return;
    }
    if (this.newSeats === b.numberOfSeats) {
      this.seatsError.set('That is the current number of seats.');
      return;
    }
    if (!this.seatsReason.trim()) {
      this.seatsError.set('Enter a reason — it is shown on the booking timeline.');
      return;
    }

    this.submitting.set(true);
    this.seatsError.set(null);
    this.bookingService.changeSeats(this.bookingId, { numberOfSeats: this.newSeats, reason: this.seatsReason.trim() }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.showSeatsModal.set(false);
        this.toast.success('Seats updated.');
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.seatsError.set(this.errorMessage(err, 'Could not change the seats.'));
      },
    });
  }

  // ---------- Travellers ----------

  get canEditTravellers(): boolean {
    const s = this.booking()?.bookingStatus;
    return s !== undefined && s !== BookingStatus.Cancelled && s !== BookingStatus.Rejected;
  }

  startEditTravellers(): void {
    const b = this.booking();
    if (!b) return;
    this.travellerRows = Array.from({ length: b.numberOfSeats }, (_, i) => {
      const t = b.travellers.find((x) => x.seatNumber === i + 1);
      return {
        seatNumber: i + 1,
        fullName: t?.fullName ?? '',
        gender: t?.gender ?? null,
        age: t?.age ?? null,
        aadhaarLast4: t?.aadhaarLast4 ?? '',
        phoneNumber: t?.phoneNumber ?? '',
      };
    });
    this.travellersError.set(null);
    this.editingTravellers.set(true);
  }

  /** Keeps only digits and the last four of whatever is typed/pasted — the full number never leaves the browser. */
  onAadhaarInput(row: TravellerRow, value: string): void {
    row.aadhaarLast4 = (value ?? '').replace(/\D/g, '').slice(-4);
  }

  saveTravellers(): void {
    const rows = this.travellerRows.filter((r) => r.fullName.trim() || r.phoneNumber.trim() || r.aadhaarLast4.trim());
    const missingName = rows.find((r) => r.fullName.trim().length < 2);
    if (missingName) {
      this.travellersError.set(`Seat ${missingName.seatNumber}: enter the traveller's full name, or clear the row.`);
      return;
    }

    const payload: TravellerInput[] = rows.map((r) => ({
      seatNumber: r.seatNumber,
      fullName: r.fullName.trim(),
      gender: r.gender,
      age: r.age === null || (r.age as unknown) === '' ? null : Number(r.age),
      aadhaarLast4: r.aadhaarLast4.trim() || null,
      phoneNumber: r.phoneNumber.trim() || null,
    }));

    this.submitting.set(true);
    this.travellersError.set(null);
    this.bookingService.updateTravellers(this.bookingId, payload).subscribe({
      next: () => {
        this.submitting.set(false);
        this.editingTravellers.set(false);
        this.toast.success('Traveller details saved.');
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.travellersError.set(this.errorMessage(err, 'Could not save traveller details.'));
      },
    });
  }

  // ---------- Male / female ----------

  openGenderModal(): void {
    const b = this.booking();
    this.maleCount = b?.maleCount ?? null;
    this.femaleCount = b?.femaleCount ?? null;
    this.genderError.set(null);
    this.showGenderModal.set(true);
  }

  submitGenderCounts(): void {
    const b = this.booking();
    if (!b) return;
    const male = this.maleCount ?? 0;
    const female = this.femaleCount ?? 0;
    if (male < 0 || female < 0 || male + female > b.numberOfSeats) {
      this.genderError.set(`Male + female must be between 0 and ${b.numberOfSeats}.`);
      return;
    }

    this.submitting.set(true);
    this.bookingService.updateGenderCounts(this.bookingId, { maleCount: this.maleCount, femaleCount: this.femaleCount }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.showGenderModal.set(false);
        this.toast.success('Male / female count saved.');
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.genderError.set(this.errorMessage(err, 'Could not save the counts.'));
      },
    });
  }

  private errorMessage(err: HttpErrorResponse, fallback: string): string {
    const body = err.error as ApiResponse<unknown> | undefined;
    return body?.errors?.join(' ') || body?.message || fallback;
  }

  get canTakePayments(): boolean {
    const s = this.booking()?.bookingStatus;
    return s === BookingStatus.Confirmed || s === BookingStatus.Completed;
  }

  openPaymentModal(): void {
    const b = this.booking();
    if (!b) return;
    this.paymentError.set(null);
    this.paymentAmount = b.remainingAmount;
    this.paymentMethod = PaymentMethod.Cash;
    this.paymentReference = '';
    this.paymentNotes = '';
    this.paymentDate = toLocalDateKey(new Date());
    this.showPaymentModal.set(true);
  }

  submitPayment(): void {
    const b = this.booking();
    if (!b) return;
    if (!(this.paymentAmount > 0) || this.paymentAmount > b.remainingAmount) {
      this.paymentError.set(`Enter an amount between ₹1 and ₹${b.remainingAmount}.`);
      return;
    }

    this.submitting.set(true);
    this.paymentError.set(null);
    this.bookingService
      .addPayment(this.bookingId, {
        amount: this.paymentAmount,
        method: this.paymentMethod,
        reference: this.paymentReference.trim() || null,
        notes: this.paymentNotes.trim() || null,
        paidAt: this.paidAtIso(),
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.showPaymentModal.set(false);
          this.toast.success('Payment recorded — the invoice now includes it.');
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          const body = err.error as ApiResponse<unknown> | undefined;
          this.paymentError.set(body?.errors?.[0] ?? body?.message ?? 'Could not record this payment.');
        },
      });
  }

  /** The picked day at the current time — "today" keeps the real time, a past day lands at noon local. */
  private paidAtIso(): string | null {
    if (!this.paymentDate) return null;
    if (this.paymentDate === toLocalDateKey(new Date())) return new Date().toISOString();
    return new Date(`${this.paymentDate}T12:00:00`).toISOString();
  }

  confirmRemovePayment(): void {
    const p = this.paymentToRemove();
    if (!p) return;
    this.submitting.set(true);
    this.bookingService.removePayment(this.bookingId, p.bookingPaymentId).subscribe({
      next: () => {
        this.submitting.set(false);
        this.paymentToRemove.set(null);
        this.toast.success('Payment removed.');
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.paymentToRemove.set(null);
        const body = err.error as ApiResponse<unknown> | undefined;
        this.toast.error(body?.message ?? 'Could not remove this payment.');
      },
    });
  }

  downloadInvoice(): void {
    this.downloadingInvoice.set(true);
    this.bookingService.downloadInvoice(this.bookingId).subscribe({
      next: (blob) => {
        this.downloadingInvoice.set(false);
        downloadFile(blob, `GhumoOdisha-Invoice-GO-${this.bookingId}.pdf`);
      },
      error: () => {
        this.downloadingInvoice.set(false);
        this.toast.error('Could not generate the invoice.');
      },
    });
  }

  get remaining(): number {
    const b = this.booking();
    if (!b) return 0;
    return Math.max(0, b.totalAmount - this.advanceAmount);
  }
}
