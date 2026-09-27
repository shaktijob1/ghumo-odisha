import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AdminRefundService } from '../../../core/services/admin-refund.service';
import { ToastService } from '../../../core/services/toast.service';
import { ApiResponse, PagedResult } from '../../../core/models/api-response.model';
import { PaymentMethod, PaymentMethodLabels, RefundStatus } from '../../../core/models/enums.model';
import { AdminRefund, RefundCounts } from '../../../core/models/refund.model';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';

type LoadState = 'loading' | 'ready' | 'error';
/** The inline form open on one refund card. */
type Action = { refundId: number; kind: 'razorpay' | 'manual' | 'settle' };

/**
 * Refund desk. Cancelling a paid booking never refunds on the spot — it lands here as Pending.
 * The admin issues it (Razorpay or a recorded manual transfer → Processing), then marks it Settled
 * once the money has reached the customer; only then does the booking show "Refunded".
 */
@Component({
  selector: 'app-refund-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent],
  templateUrl: './refund-list.component.html',
})
export class RefundListComponent implements OnInit {
  private readonly refunds = inject(AdminRefundService);
  private readonly toast = inject(ToastService);

  readonly RefundStatus = RefundStatus;
  readonly PaymentMethod = PaymentMethod;
  readonly PaymentMethodLabels = PaymentMethodLabels;
  readonly manualMethods = [PaymentMethod.Upi, PaymentMethod.BankTransfer, PaymentMethod.Cash, PaymentMethod.Other];

  readonly tab = signal<RefundStatus>(RefundStatus.Pending);
  readonly state = signal<LoadState>('loading');
  readonly result = signal<PagedResult<AdminRefund> | null>(null);
  readonly counts = signal<RefundCounts | null>(null);
  readonly search = signal('');
  readonly page = signal(1);
  readonly pageSize = 20;

  readonly action = signal<Action | null>(null);
  readonly busy = signal(false);
  readonly actionError = signal<string | null>(null);
  /** Live Razorpay status per refund id, fetched on demand. */
  readonly gatewayStatus = signal<Record<number, string>>({});

  amount = 0;
  method: PaymentMethod = PaymentMethod.Upi;
  reference = '';
  notes = '';

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.refunds.getCounts().subscribe({ next: (c) => this.counts.set(c), error: () => undefined });
    this.refunds.getRefunds(this.tab(), this.search(), this.page(), this.pageSize).subscribe({
      next: (r) => {
        this.result.set(r);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  selectTab(status: RefundStatus): void {
    if (this.tab() === status) return;
    this.tab.set(status);
    this.page.set(1);
    this.action.set(null);
    this.load();
  }

  onSearchChange(value: string): void {
    this.search.set(value);
    this.page.set(1);
    this.load();
  }

  goToPage(page: number): void {
    this.page.set(page);
    this.load();
  }

  get totalPages(): number {
    const r = this.result();
    return r ? Math.max(1, Math.ceil(r.totalCount / this.pageSize)) : 1;
  }

  /** The most a Razorpay refund can return: what was paid online, never more than was paid overall. */
  maxOnline(r: AdminRefund): number {
    return Math.min(r.onlinePaidAmount, r.amountPaid);
  }

  isOpen(r: AdminRefund, kind: Action['kind']): boolean {
    const a = this.action();
    return !!a && a.refundId === r.refundId && a.kind === kind;
  }

  open(r: AdminRefund, kind: Action['kind']): void {
    this.action.set({ refundId: r.refundId, kind });
    this.actionError.set(null);
    this.amount = kind === 'razorpay' ? this.maxOnline(r) : r.amountPaid;
    this.method = PaymentMethod.Upi;
    this.reference = '';
    this.notes = '';
  }

  close(): void {
    this.action.set(null);
  }

  issueRazorpay(r: AdminRefund): void {
    if (!(this.amount > 0) || this.amount > this.maxOnline(r)) {
      this.actionError.set(`Enter an amount between ₹1 and ₹${this.maxOnline(r)}.`);
      return;
    }
    this.run(this.refunds.issueRazorpay(r.refundId, this.amount, this.notes.trim() || null), 'Razorpay refund issued — mark it settled once it shows as processed.');
  }

  recordManual(r: AdminRefund): void {
    if (!(this.amount > 0) || this.amount > r.amountPaid) {
      this.actionError.set(`Enter an amount between ₹1 and ₹${r.amountPaid}.`);
      return;
    }
    this.run(
      this.refunds.recordManual(r.refundId, this.amount, this.method, this.reference.trim() || null, this.notes.trim() || null),
      'Manual refund recorded — mark it settled once the customer has received it.',
    );
  }

  settle(r: AdminRefund): void {
    this.run(this.refunds.settle(r.refundId, this.notes.trim() || null), 'Refund settled — the customer now sees it as refunded.');
  }

  checkGateway(r: AdminRefund): void {
    this.refunds.gatewayStatus(r.refundId).subscribe({
      next: (s) => this.gatewayStatus.set({ ...this.gatewayStatus(), [r.refundId]: s ?? 'unknown' }),
      error: () => this.toast.error('Could not reach Razorpay right now.'),
    });
  }

  private run(request: Observable<AdminRefund>, successMessage: string): void {
    this.busy.set(true);
    this.actionError.set(null);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        this.action.set(null);
        this.toast.success(successMessage);
        this.load();
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        const body = err.error as ApiResponse<unknown> | undefined;
        this.actionError.set(body?.message && body.message !== 'One or more validation errors occurred.'
          ? body.message
          : body?.errors?.join(' ') ?? 'Something went wrong. Please try again.');
      },
    });
  }
}
