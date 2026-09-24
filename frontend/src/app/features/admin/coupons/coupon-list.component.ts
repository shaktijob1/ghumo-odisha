import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminCouponService } from '../../../core/services/admin-coupon.service';
import { AdminCoupon, AdminCouponBooking } from '../../../core/models/coupon.model';
import { ApiResponse } from '../../../core/models/api-response.model';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';

type LoadState = 'loading' | 'ready' | 'error';

const DEFAULT_DISCOUNT = 199;
const DEFAULT_COMMISSION_PER_SEAT = 200;

@Component({
  selector: 'app-coupon-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent, StatusBadgeComponent],
  templateUrl: './coupon-list.component.html',
})
export class CouponListComponent implements OnInit {
  private readonly couponService = inject(AdminCouponService);

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
