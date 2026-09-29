import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminPendingPricing } from '../../../core/models/admin-car.model';
import { ApprovalLabels } from '../../../core/models/car.model';
import { AdminCarService } from '../../../core/services/admin-car.service';
import { ToastService } from '../../../core/services/toast.service';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { apiErrorMessage } from '../../../shared/utils/api-error';
import { istDateTime } from '../../../shared/utils/car-format';
import { DecisionDialogComponent } from './decision-dialog.component';
import { PricingSummaryComponent } from './pricing-summary.component';

type LoadState = 'loading' | 'ready' | 'error';

/** Pricing proposals waiting for a decision, each shown next to the car's current live pricing. */
@Component({
  selector: 'app-admin-pricing-queue',
  standalone: true,
  imports: [RouterLink, StatePanelComponent, DecisionDialogComponent, PricingSummaryComponent],
  template: `
    <a class="aback" routerLink="/admin/cars">← Cars</a>
    <div class="ahead">
      <div>
        <h2>Pricing approvals</h2>
        <div class="sub">Drivers' price changes. The current price stays live until you approve the new one.</div>
      </div>
    </div>

    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <app-state-panel kind="error" message="Could not load pricing proposals."></app-state-panel> }
      @case ('ready') {
        @if (items().length === 0) {
          <app-state-panel kind="empty" message="No pricing waiting for approval."></app-state-panel>
        }
        @for (it of items(); track it.proposed.carPricingId) {
          <div class="panel" style="margin-bottom:12px">
            <div class="row sp" style="align-items:flex-start;gap:12px;flex-wrap:wrap">
              <div>
                <a [routerLink]="['/admin/cars', it.carId]"><b>{{ it.carDisplayName }}</b></a>
                <div class="note">
                  <span class="mono">{{ it.registrationNumber }}</span> · {{ it.category }} · car {{ carLabels[it.carStatus].toLowerCase() }} ·
                  driver <a [routerLink]="['/admin/drivers', it.driverId]">{{ it.driverName }}</a>
                </div>
              </div>
              <div class="note">Sent {{ when(it.proposed.submittedAt) }}</div>
            </div>
            <div class="acar-pricing" style="margin-top:12px">
              <div>
                <div class="lbl">Current (live)</div>
                @if (it.current; as cur) { <app-pricing-summary [pricing]="cur"></app-pricing-summary> } @else { <p class="note">None yet. This is the car's first pricing.</p> }
              </div>
              <div class="acar-proposal">
                <div class="lbl">Proposed</div>
                <app-pricing-summary [pricing]="it.proposed"></app-pricing-summary>
              </div>
            </div>
            @if (rowError()[it.proposed.carPricingId]; as e) { <div class="errorbox">{{ e }}</div> }
            <div class="row" style="gap:8px;justify-content:flex-end;margin-top:12px">
              <button class="btn sm dang" (click)="rejecting.set(it); error.set(null)">Reject</button>
              <button class="btn sm" [disabled]="approving() === it.proposed.carPricingId" (click)="approve(it)">
                @if (approving() === it.proposed.carPricingId) { <span class="spin"></span> } @else { Approve }
              </button>
            </div>
          </div>
        }
      }
    }

    @if (rejecting(); as it) {
      <app-decision-dialog
        title="Reject pricing"
        [message]="'The current pricing for ' + it.carDisplayName + ' stays live. The driver sees your reason and can propose again.'"
        confirmLabel="Reject pricing"
        reasonLabel="Reason (the driver will see this)"
        [reasonRequired]="true"
        [danger]="true"
        [busy]="busy()"
        [error]="error()"
        (confirmed)="reject(it, $event!)"
        (closed)="rejecting.set(null)"
      ></app-decision-dialog>
    }
  `,
})
export class AdminPricingQueueComponent implements OnInit {
  private readonly cars = inject(AdminCarService);
  private readonly toast = inject(ToastService);

  readonly carLabels = ApprovalLabels.car;
  readonly when = istDateTime;

  readonly state = signal<LoadState>('loading');
  readonly items = signal<AdminPendingPricing[]>([]);
  readonly approving = signal<number | null>(null);
  readonly rowError = signal<Record<number, string>>({});
  readonly rejecting = signal<AdminPendingPricing | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.cars.pendingPricing().subscribe({
      next: (items) => {
        this.items.set(items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  approve(it: AdminPendingPricing): void {
    const id = it.proposed.carPricingId;
    this.approving.set(id);
    this.rowError.update((e) => ({ ...e, [id]: '' }));
    this.cars.approvePricing(id).subscribe({
      next: () => {
        this.approving.set(null);
        this.toast.success(`Pricing approved for ${it.carDisplayName}.`);
        this.removed(id);
      },
      error: (err) => {
        this.approving.set(null);
        this.rowError.update((e) => ({ ...e, [id]: apiErrorMessage(err, 'Could not approve this pricing.') }));
      },
    });
  }

  reject(it: AdminPendingPricing, reason: string): void {
    this.busy.set(true);
    this.error.set(null);
    this.cars.rejectPricing(it.proposed.carPricingId, reason).subscribe({
      next: () => {
        this.busy.set(false);
        this.rejecting.set(null);
        this.toast.success('Pricing rejected.');
        this.removed(it.proposed.carPricingId);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(apiErrorMessage(err, 'Could not reject this pricing.'));
      },
    });
  }

  private removed(pricingId: number): void {
    this.items.update((list) => list.filter((i) => i.proposed.carPricingId !== pricingId));
    this.cars.dashboard().subscribe({ error: () => undefined });
  }
}
