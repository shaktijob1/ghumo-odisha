import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { PartnerCouponSummary, PartnerService } from '../../../core/services/partner.service';
import { ContactService } from '../../../core/services/contact.service';
import { PaymentMethod } from '../../../core/models/enums.model';
import { CountUpDirective } from '../../../shared/directives/count-up.directive';

type ViewState = 'idle' | 'loading' | 'ready' | 'notfound' | 'error';

const LAST_CODE_KEY = 'go_partner_code';

/**
 * Public partner / influencer earnings page (/admin/influencer — lives with the admin screens but
 * has no login). A coupon holder types their code and sees their own bookings count, commission,
 * payouts and balance. The API returns nothing about who booked.
 */
@Component({
  selector: 'app-influencer-portal',
  standalone: true,
  imports: [CommonModule, FormsModule, CountUpDirective],
  templateUrl: './influencer-portal.component.html',
  // Split by section to stay within the per-stylesheet size budget.
  styleUrls: ['./influencer-portal.component.css', './influencer-portal.results.css', './influencer-portal.lists.css'],
})
export class InfluencerPortalComponent implements OnInit {
  private readonly partnerService = inject(PartnerService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly contact = inject(ContactService).get();

  readonly state = signal<ViewState>('idle');
  readonly summary = signal<PartnerCouponSummary | null>(null);
  readonly errorMessage = signal<string | null>(null);
  code = '';

  readonly firstName = computed(() => this.summary()?.holderName.trim().split(/\s+/)[0] ?? '');

  /** Share of earned commission already paid out, for the progress ring (0–100). */
  readonly paidPercent = computed(() => {
    const s = this.summary();
    if (!s || s.commissionEarned <= 0) return s && s.commissionPaid > 0 ? 100 : 0;
    return Math.min(100, Math.round((s.commissionPaid / s.commissionEarned) * 100));
  });

  // Ring geometry: r = 52 → circumference ≈ 326.7.
  readonly ringCircumference = 2 * Math.PI * 52;
  readonly ringOffset = computed(() => this.ringCircumference * (1 - this.paidPercent() / 100));

  ngOnInit(): void {
    const fromLink = this.route.snapshot.queryParamMap.get('code');
    const remembered = this.readRemembered();
    const initial = (fromLink || remembered || '').trim().toUpperCase();
    if (initial) {
      this.code = initial;
      this.lookup();
    }
  }

  lookup(): void {
    const code = this.code.trim().toUpperCase();
    if (!code || this.state() === 'loading') return;
    this.code = code;
    this.state.set('loading');
    this.errorMessage.set(null);

    this.partnerService.getSummary(code).subscribe({
      next: (s) => {
        this.summary.set(s);
        this.state.set('ready');
        this.remember(s.code);
        // Shareable, bookmarkable link: /admin/influencer?code=XYZ
        this.router.navigate([], { queryParams: { code: s.code }, replaceUrl: true });
      },
      error: (err: HttpErrorResponse) => {
        this.summary.set(null);
        if (err.status === 404) {
          this.state.set('notfound');
          this.errorMessage.set(err.error?.message ?? "We couldn't find that coupon code.");
        } else if (err.status === 429) {
          this.state.set('error');
          this.errorMessage.set('Too many tries — please wait a minute and try again.');
        } else {
          this.state.set('error');
          this.errorMessage.set('Something went wrong. Please try again in a moment.');
        }
      },
    });
  }

  checkAnother(): void {
    this.summary.set(null);
    this.state.set('idle');
    this.code = '';
    this.errorMessage.set(null);
    this.forget();
    this.router.navigate([], { queryParams: {}, replaceUrl: true });
  }

  methodLabel(method: PaymentMethod): string {
    switch (method) {
      case PaymentMethod.Upi:
        return 'UPI';
      case PaymentMethod.BankTransfer:
        return 'Bank transfer';
      case PaymentMethod.Cash:
        return 'Cash';
      case PaymentMethod.Razorpay:
        return 'Razorpay';
      default:
        return 'Other';
    }
  }

  // Remembering the code is a per-browser convenience only — it can fail (private mode) harmlessly.
  private readRemembered(): string | null {
    try {
      return localStorage.getItem(LAST_CODE_KEY);
    } catch {
      return null;
    }
  }

  private remember(code: string): void {
    try {
      localStorage.setItem(LAST_CODE_KEY, code);
    } catch {
      /* ignore */
    }
  }

  private forget(): void {
    try {
      localStorage.removeItem(LAST_CODE_KEY);
    } catch {
      /* ignore */
    }
  }
}
