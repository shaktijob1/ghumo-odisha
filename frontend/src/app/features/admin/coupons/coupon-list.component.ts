import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminCouponService } from '../../../core/services/admin-coupon.service';
import { AdminCoupon, AdminCouponBooking, CouponPayout } from '../../../core/models/coupon.model';
import { PaymentMethod } from '../../../core/models/enums.model';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog.component';
import { toLocalDateKey } from '../../../shared/utils/date-key';
import { ToastService } from '../../../core/services/toast.service';
import { ApiResponse } from '../../../core/models/api-response.model';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';

type LoadState = 'loading' | 'ready' | 'error';

const DEFAULT_DISCOUNT = 199;
const DEFAULT_COMMISSION_PER_SEAT = 200;

@Component({
  selector: 'app-coupon-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent, StatusBadgeComponent, ConfirmDialogComponent],
  templateUrl: './coupon-list.component.html',
})
export class CouponListComponent implements OnInit {
  private readonly couponService = inject(AdminCouponService);
  private readonly toast = inject(ToastService);

  readonly state = signal<LoadState>('loading');
  readonly coupons = signal<AdminCoupon[]>([]);

  readonly code = signal('');
  readonly holderName = signal('');
  readonly discountAmount = signal(DEFAULT_DISCOUNT);
  readonly commissionPerSeat = signal(DEFAULT_COMMISSION_PER_SEAT);
  readonly validFrom = signal('');
  readonly validUntil = signal('');
  readonly isFirstTimeCustomerOnly = signal(false);
  readonly creating = signal(false);
  readonly createError = signal<string | null>(null);

  readonly selectedCouponId = signal<number | null>(null);
  readonly selectedCoupon = computed(() => this.coupons().find((c) => c.couponCodeId === this.selectedCouponId()) ?? null);
  readonly bookingsState = signal<LoadState>('loading');
  readonly bookings = signal<AdminCouponBooking[]>([]);
  readonly cancelledBookings = computed(() => this.bookings().filter((b) => b.commissionEarned !== b.commissionAmount));

  readonly payoutsState = signal<LoadState>('loading');
  readonly payouts = signal<CouponPayout[]>([]);
  readonly showPayoutModal = signal(false);
  readonly payoutError = signal<string | null>(null);
  readonly savingPayout = signal(false);
  readonly payoutToRemove = signal<CouponPayout | null>(null);
  payoutAmount = 0;
  payoutMethod: PaymentMethod = PaymentMethod.Upi;
  payoutReference = '';
  payoutNotes = '';
  payoutDate = '';

  // Payouts are sent by hand (no RazorpayX automation) — "Razorpay" here means a manual transfer made there.
  readonly payoutMethods: { value: PaymentMethod; label: string }[] = [
    { value: PaymentMethod.Upi, label: 'UPI' },
    { value: PaymentMethod.BankTransfer, label: 'Bank transfer' },
    { value: PaymentMethod.Cash, label: 'Cash' },
    { value: PaymentMethod.Razorpay, label: 'Razorpay (manual transfer)' },
    { value: PaymentMethod.Other, label: 'Other' },
  ];

  /** Link to the public partner earnings page, pre-filled with this coupon's code. */
  copyPartnerLink(code: string): void {
    const url = `${window.location.origin}/admin/influencer?code=${encodeURIComponent(code)}`;
    navigator.clipboard?.writeText(url).then(
      () => this.toast.success('Partner link copied — share it with the coupon holder.'),
      () => this.toast.info(url),
    ) ?? this.toast.info(url);
  }

  payoutMethodLabel(method: PaymentMethod): string {
    return this.payoutMethods.find((m) => m.value === method)?.label ?? 'Other';
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.couponService.getCoupons().subscribe({
      next: (r) => {
        this.coupons.set(r);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  create(): void {
    if (!this.code().trim() || !this.holderName().trim() || this.discountAmount() <= 0) {
      this.createError.set('Enter a code, the coupon holder name and a discount amount greater than 0.');
      return;
    }
    if (this.commissionPerSeat() == null || this.commissionPerSeat() < 0) {
      this.createError.set('Commission per seat cannot be negative.');
      return;
    }

    this.creating.set(true);
    this.createError.set(null);
    this.couponService
      .createCoupon({
        code: this.code().trim(),
        holderName: this.holderName().trim(),
        discountAmount: this.discountAmount(),
        commissionPerSeat: this.commissionPerSeat(),
        validFrom: this.validFrom() || null,
        validUntil: this.validUntil() || null,
        isFirstTimeCustomerOnly: this.isFirstTimeCustomerOnly(),
      })
      .subscribe({
        next: () => {
          this.creating.set(false);
          this.code.set('');
          this.holderName.set('');
          this.discountAmount.set(DEFAULT_DISCOUNT);
          this.commissionPerSeat.set(DEFAULT_COMMISSION_PER_SEAT);
          this.validFrom.set('');
          this.validUntil.set('');
          this.isFirstTimeCustomerOnly.set(false);
          this.load();
        },
        error: (err: HttpErrorResponse) => {
          this.creating.set(false);
          const body = err.error as ApiResponse<unknown> | undefined;
          this.createError.set(body?.errors?.[0] ?? body?.message ?? 'Could not create the coupon.');
        },
      });
  }

  toggleActive(coupon: AdminCoupon): void {
    this.couponService
      .updateCoupon(coupon.couponCodeId, {
        holderName: coupon.holderName,
        discountAmount: coupon.discountAmount,
        commissionPerSeat: coupon.commissionPerSeat,
        validFrom: coupon.validFrom,
        validUntil: coupon.validUntil,
        isActive: !coupon.isActive,
        isFirstTimeCustomerOnly: coupon.isFirstTimeCustomerOnly,
      })
      .subscribe({ next: () => this.load() });
  }

  remove(coupon: AdminCoupon): void {
    if (this.selectedCouponId() === coupon.couponCodeId) {
      this.selectedCouponId.set(null);
    }
    this.couponService.deleteCoupon(coupon.couponCodeId).subscribe({ next: () => this.load() });
  }

  toggleBookings(coupon: AdminCoupon): void {
    if (this.selectedCouponId() === coupon.couponCodeId) {
      this.selectedCouponId.set(null);
      return;
    }

    this.selectedCouponId.set(coupon.couponCodeId);
    this.loadBookings(coupon.couponCodeId);
    this.loadPayouts(coupon.couponCodeId);
  }

  loadPayouts(couponCodeId: number): void {
    this.payoutsState.set('loading');
    this.payouts.set([]);
    this.couponService.getPayouts(couponCodeId).subscribe({
      next: (r) => {
        if (this.selectedCouponId() !== couponCodeId) return;
        this.payouts.set(r);
        this.payoutsState.set('ready');
      },
      error: () => {
        if (this.selectedCouponId() === couponCodeId) this.payoutsState.set('error');
      },
    });
  }

  openPayoutModal(): void {
    const c = this.selectedCoupon();
    if (!c) return;
    this.payoutAmount = Math.max(0, c.balanceDue);
    this.payoutMethod = PaymentMethod.Upi;
    this.payoutReference = '';
    this.payoutNotes = '';
    this.payoutDate = toLocalDateKey(new Date());
    this.payoutError.set(null);
    this.showPayoutModal.set(true);
  }

  savePayout(): void {
    const c = this.selectedCoupon();
    if (!c) return;
    if (!(this.payoutAmount > 0) || this.payoutAmount > c.balanceDue) {
      this.payoutError.set(`Enter an amount between ₹1 and ₹${c.balanceDue}.`);
      return;
    }

    const today = toLocalDateKey(new Date());
    const paidAt = !this.payoutDate
      ? null
      : this.payoutDate === today
        ? new Date().toISOString()
        : new Date(`${this.payoutDate}T12:00:00`).toISOString();

    this.savingPayout.set(true);
    this.payoutError.set(null);
    this.couponService
      .addPayout(c.couponCodeId, {
        amount: this.payoutAmount,
        method: this.payoutMethod,
        reference: this.payoutReference.trim() || null,
        notes: this.payoutNotes.trim() || null,
        paidAt,
      })
      .subscribe({
        next: () => {
          this.savingPayout.set(false);
          this.showPayoutModal.set(false);
          this.refreshSelected(c.couponCodeId);
        },
        error: (err: HttpErrorResponse) => {
          this.savingPayout.set(false);
          const body = err.error as ApiResponse<unknown> | undefined;
          this.payoutError.set(body?.errors?.[0] ?? body?.message ?? 'Could not record the payout.');
        },
      });
  }

  confirmRemovePayout(): void {
    const c = this.selectedCoupon();
    const p = this.payoutToRemove();
    if (!c || !p) return;
    this.couponService.removePayout(c.couponCodeId, p.couponPayoutId).subscribe({
      next: () => {
        this.payoutToRemove.set(null);
        this.refreshSelected(c.couponCodeId);
      },
      error: () => this.payoutToRemove.set(null),
    });
  }

  /** Reloads the totals row and the payout list without collapsing the open coupon. */
  private refreshSelected(couponCodeId: number): void {
    this.couponService.getCoupons().subscribe((r) => this.coupons.set(r));
    this.loadPayouts(couponCodeId);
  }

  loadBookings(couponCodeId: number): void {
    this.bookingsState.set('loading');
    this.bookings.set([]);
    this.couponService.getCouponBookings(couponCodeId).subscribe({
      next: (r) => {
        // Ignore a late response if the admin has already switched to another coupon.
        if (this.selectedCouponId() !== couponCodeId) return;
        this.bookings.set(r);
        this.bookingsState.set('ready');
      },
      error: () => {
        if (this.selectedCouponId() === couponCodeId) this.bookingsState.set('error');
      },
    });
  }
}
