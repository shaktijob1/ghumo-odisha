import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminCouponService } from '../../../core/services/admin-coupon.service';
import { AdminCoupon } from '../../../core/models/coupon.model';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-coupon-list',
  standalone: true,
  imports: [CommonModule, FormsModule, StatePanelComponent],
  templateUrl: './coupon-list.component.html',
})
export class CouponListComponent implements OnInit {
  private readonly couponService = inject(AdminCouponService);

  readonly state = signal<LoadState>('loading');
  readonly coupons = signal<AdminCoupon[]>([]);

  readonly code = signal('');
  readonly discountAmount = signal(199);
  readonly validFrom = signal('');
  readonly validUntil = signal('');
  readonly creating = signal(false);
  readonly createError = signal<string | null>(null);

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
    if (!this.code().trim() || this.discountAmount() <= 0) {
      this.createError.set('Enter a code and a discount amount greater than 0.');
      return;
    }

    this.creating.set(true);
    this.createError.set(null);
    this.couponService
      .createCoupon({
        code: this.code().trim(),
        discountAmount: this.discountAmount(),
        validFrom: this.validFrom() || null,
        validUntil: this.validUntil() || null,
      })
      .subscribe({
        next: () => {
          this.creating.set(false);
          this.code.set('');
          this.discountAmount.set(199);
          this.validFrom.set('');
          this.validUntil.set('');
          this.load();
        },
        error: () => this.creating.set(false),
      });
  }

  toggleActive(coupon: AdminCoupon): void {
    this.couponService
      .updateCoupon(coupon.couponCodeId, {
        discountAmount: coupon.discountAmount,
        validFrom: coupon.validFrom,
        validUntil: coupon.validUntil,
        isActive: !coupon.isActive,
      })
      .subscribe({ next: () => this.load() });
  }

  remove(coupon: AdminCoupon): void {
    this.couponService.deleteCoupon(coupon.couponCodeId).subscribe({ next: () => this.load() });
  }
}
