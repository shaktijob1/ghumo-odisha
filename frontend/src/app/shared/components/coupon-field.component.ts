import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { PaymentService } from '../../core/services/payment.service';

/** What the parent should apply: a validated discount, or a code waiting for sign-in. */
export interface CouponSelection {
  code: string;
  discountAmount: number;
  /** False while the customer isn't signed in yet — the code is checked after WhatsApp verification. */
  validated: boolean;
}

/**
 * "Have a coupon code?" field. Signed in: validates against the API right away (existence,
 * validity dates, one use per customer, first-time-only). Signed out: remembers the code and
 * says it will be applied after WhatsApp verification, because those checks need to know who
 * the customer is. The API re-validates the code again when the payment order is created.
 */
@Component({
  selector: 'app-coupon-field',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="bp-coupon">
      <div class="bp-coupon-head">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20.59 13.41 11 3.83A2 2 0 0 0 9.57 3H4a1 1 0 0 0-1 1v5.57a2 2 0 0 0 .83 1.42l9.59 9.58a2 2 0 0 0 2.83 0l4.34-4.34a2 2 0 0 0 0-2.82z"></path><path d="M7.5 7.5h.01"></path></svg>
        <b>Have a coupon code?</b>
      </div>
      @if (selection; as s) {
        <div class="bp-coupon-applied" [class.pending]="!s.validated">
          <span>
            @if (s.validated) {
              <b>{{ s.code }}</b> applied — ₹{{ s.discountAmount | number: '1.0-0' }} off the remaining amount
            } @else {
              <b>{{ s.code }}</b> will be applied after you verify your WhatsApp number
            }
          </span>
          <button type="button" class="linkbtn" (click)="remove()">Remove</button>
        </div>
      } @else {
        <div class="bp-coupon-row">
          <input
            class="inp"
            placeholder="Enter coupon code"
            maxlength="32"
            autocapitalize="characters"
            [ngModel]="input()"
            (ngModelChange)="input.set(($event ?? '').toUpperCase())"
            (keydown.enter)="apply()"
          />
          <button type="button" class="btn sm soft" [disabled]="checking() || !input().trim()" (click)="apply()">
            @if (checking()) { <span class="spin"></span> } @else { Apply }
          </button>
        </div>
      }
      @if (error()) {
        <p class="bp-coupon-error">{{ error() }}</p>
      }
    </div>
  `,
})
export class CouponFieldComponent implements OnChanges {
  private readonly paymentService = inject(PaymentService);

  @Input() signedIn = false;
  /** Current selection, owned by the parent. */
  @Input() selection: CouponSelection | null = null;
  /** Shown under the field — e.g. why a remembered code couldn't be applied after sign-in. */
  @Input() externalError: string | null = null;
  @Output() selectionChange = new EventEmitter<CouponSelection | null>();

  readonly input = signal('');
  readonly checking = signal(false);
  readonly error = signal<string | null>(null);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['externalError']) {
      this.error.set(this.externalError);
    }
  }

  apply(): void {
    const code = this.input().trim().toUpperCase();
    if (!code || this.checking()) return;
    this.error.set(null);

    if (!this.signedIn) {
      this.selectionChange.emit({ code, discountAmount: 0, validated: false });
      return;
    }

    this.checking.set(true);
    this.paymentService.validateCoupon(code).subscribe({
      next: (r) => {
        this.checking.set(false);
        this.selectionChange.emit({ code: r.code, discountAmount: r.discountAmount, validated: true });
      },
      error: (err) => {
        this.checking.set(false);
        this.error.set(err?.error?.message || 'Could not apply this coupon.');
      },
    });
  }

  remove(): void {
    this.input.set('');
    this.error.set(null);
    this.selectionChange.emit(null);
  }
}
