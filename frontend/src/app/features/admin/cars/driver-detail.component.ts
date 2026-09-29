import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminDecision, AdminDriverDetail } from '../../../core/models/admin-car.model';
import { ApprovalLabels, CarStatus, DriverStatus, approvalBadgeClass } from '../../../core/models/car.model';
import { AdminCarService } from '../../../core/services/admin-car.service';
import { ToastService } from '../../../core/services/toast.service';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { apiErrorMessage } from '../../../shared/utils/api-error';
import { istDateTime } from '../../../shared/utils/car-format';
import { AdminDocumentsComponent } from './admin-documents.component';
import { AuditHistoryComponent } from './audit-history.component';
import { DecisionDialogComponent } from './decision-dialog.component';

type LoadState = 'loading' | 'ready' | 'error';
type DriverDecision = Exclude<AdminDecision, 'deactivate'>;

@Component({
  selector: 'app-admin-driver-detail',
  standalone: true,
  imports: [DatePipe, RouterLink, ImageUrlPipe, StatePanelComponent, AdminDocumentsComponent, AuditHistoryComponent, DecisionDialogComponent],
  template: `
    <a class="aback" routerLink="/admin/drivers">← Drivers</a>
    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <app-state-panel kind="error" message="Could not load this driver."></app-state-panel> }
      @case ('ready') {
        @let d = detail()!;
        @let p = d.profile;

        <div class="ahead">
          <div class="row" style="gap:14px">
            @if (p.profilePhotoUrl) {
              <img class="acar-av lg" [src]="p.profilePhotoUrl | imageUrl" alt="" />
            } @else {
              <span class="acar-av lg acar-av-empty">{{ p.name.charAt(0) }}</span>
            }
            <div>
              <h2>{{ p.name }}</h2>
              <div class="sub">
                <span [class]="'badge ' + badge()">{{ awaitingReview() ? 'Waiting for review' : labels[p.status] }}</span>
                Driver #{{ p.driverId }} · joined {{ p.createdAt | date: 'd MMM y' }}
              </div>
            </div>
          </div>
          <div class="row" style="gap:8px;flex-wrap:wrap">
            @if (p.status !== DriverStatus.Suspended) {
              <a class="btn sm ghost" routerLink="/admin/cars/new" [queryParams]="{ driver: p.driverId }">Add car</a>
            }
            @if (canReject()) { <button class="btn sm dang" (click)="ask('reject')">Reject</button> }
            @if (canSuspend()) { <button class="btn sm dang" (click)="ask('suspend')">Suspend</button> }
            @if (canApprove()) {
              <button class="btn sm" (click)="ask('approve')">{{ p.status === DriverStatus.Suspended ? 'Lift suspension' : 'Approve driver' }}</button>
            }
          </div>
        </div>

        @if (p.statusReason && p.status !== DriverStatus.Approved) {
          <div class="panel acar-reason"><b>{{ p.status === DriverStatus.Suspended ? 'Suspended' : 'Rejected' }}:</b> {{ p.statusReason }}</div>
        }
        @if (addedAndPending()) {
          <div class="panel acar-missing">
            <b>Added by admin.</b> You can approve this driver now; their cars go live once approved. They can complete their profile and licence later by signing in with their WhatsApp number.
          </div>
        } @else if (p.missingForReview.length) {
          <div class="panel acar-missing">
            <b>Profile not complete</b>
            <ul>@for (m of p.missingForReview; track m) { <li>{{ m }}</li> }</ul>
          </div>
        }

        <div class="panels">
          <div class="panel">
            <h4>Profile</h4>
            <div class="kv" style="margin-top:10px">
              <span class="k">WhatsApp</span><b>{{ p.phoneNumber ? '+91 ' + p.phoneNumber : '—' }}</b>
              <span class="k">Email</span><b>{{ p.email || '—' }}</b>
              <span class="k">Google account</span><b>{{ d.googleEmail || '—' }}</b>
              <span class="k">Address</span><b>{{ p.address || '—' }}</b>
              <span class="k">City</span><b>{{ p.city || '—' }}</b>
              <span class="k">Experience</span><b>{{ p.experienceYears !== null ? p.experienceYears + ' years' : '—' }}</b>
              <span class="k">Licence number</span><b class="mono">{{ p.drivingLicenceNumber || '—' }}</b>
              <span class="k">Licence valid until</span><b>{{ p.licenceExpiryDate ? (p.licenceExpiryDate | date: 'd MMM y') : '—' }}</b>
              <span class="k">Sent for review</span><b>{{ p.submittedForReviewAt ? when(p.submittedForReviewAt) : '—' }}</b>
              <span class="k">Approved</span><b>{{ p.approvedAt ? when(p.approvedAt) : '—' }}</b>
            </div>
          </div>
          <div class="panel">
            <h4>Documents</h4>
            <div class="sub">Private: only admins and this driver can open them.</div>
            <div style="margin-top:10px"><app-admin-documents [documents]="driverDocuments()"></app-admin-documents></div>
          </div>
        </div>

        <div class="panel" style="margin-bottom:14px">
          <h4>Cars</h4>
          @if (d.cars.length === 0) {
            <p class="note" style="margin-top:8px">No cars registered yet.</p>
          } @else {
            <div class="tblwrap" style="margin-top:10px">
              <table class="tbl">
                <tr><th>Car</th><th>Number plate</th><th>Status</th><th>In search</th></tr>
                @for (c of d.cars; track c.carId) {
                  <tr>
                    <td><a [routerLink]="['/admin/cars', c.carId]"><b>{{ c.displayName }}</b></a><div class="note">{{ c.category }}</div></td>
                    <td class="mono">{{ c.registrationNumber }}</td>
                    <td>
                      <span [class]="'badge ' + carBadge(c.status)">{{ c.awaitingReview ? 'Waiting for review' : carLabels[c.status] }}</span>
                      @if (c.hasPendingPricing) { <span class="badge wait" style="margin-left:4px">New pricing</span> }
                    </td>
                    <td>@if (c.isListed) { <span class="badge ok">Live</span> } @else { <span class="note">No</span> }</td>
                  </tr>
                }
              </table>
            </div>
          }
        </div>

        <div class="panel">
          <h4>History</h4>
          <div style="margin-top:10px"><app-audit-history [events]="d.history"></app-audit-history></div>
        </div>
      }
    }

    @if (decision(); as dec) {
      <app-decision-dialog
        [title]="dialogTitle(dec)"
        [message]="dialogMessage(dec)"
        [confirmLabel]="dialogTitle(dec)"
        [reasonLabel]="dec === 'approve' ? 'Note' : 'Reason (the driver will see this)'"
        [reasonRequired]="dec !== 'approve'"
        [danger]="dec !== 'approve'"
        [busy]="busy()"
        [error]="error()"
        (confirmed)="decide(dec, $event)"
        (closed)="decision.set(null)"
      ></app-decision-dialog>
    }
  `,
})
export class AdminDriverDetailComponent implements OnInit {
  private readonly cars = inject(AdminCarService);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);

  readonly labels = ApprovalLabels.driver;
  readonly carLabels = ApprovalLabels.car;
  readonly DriverStatus = DriverStatus;
  readonly when = istDateTime;

  readonly state = signal<LoadState>('loading');
  readonly detail = signal<AdminDriverDetail | null>(null);
  readonly decision = signal<DriverDecision | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  private readonly profile = computed(() => this.detail()?.profile ?? null);
  /** Car documents (RC, insurance) are shown on the car's own page. */
  readonly driverDocuments = computed(() => (this.profile()?.documents ?? []).filter((d) => d.carId === null));
  readonly awaitingReview = computed(() => {
    const p = this.profile();
    return !!p && p.status === DriverStatus.Pending && !!p.submittedForReviewAt;
  });
  /** A driver the admin added can be approved straight away, without a submitted profile. */
  readonly addedAndPending = computed(() => !!this.detail()?.addedByAdmin && this.profile()?.status === DriverStatus.Pending);
  readonly canApprove = computed(() => this.awaitingReview() || this.addedAndPending() || this.profile()?.status === DriverStatus.Suspended);
  readonly canReject = computed(() => this.awaitingReview());
  readonly canSuspend = computed(() => this.profile()?.status === DriverStatus.Approved);
  readonly badge = computed(() => {
    const s = this.profile()?.status ?? DriverStatus.Pending;
    return approvalBadgeClass(s, DriverStatus.Approved, [DriverStatus.Rejected, DriverStatus.Suspended]);
  });

  private get id(): number {
    return Number(this.route.snapshot.paramMap.get('id'));
  }

  ngOnInit(): void {
    this.cars.driver(this.id).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  ask(decision: DriverDecision): void {
    this.error.set(null);
    this.decision.set(decision);
  }

  decide(decision: DriverDecision, reason: string | null): void {
    this.busy.set(true);
    this.error.set(null);
    this.cars.decideDriver(this.id, decision, reason).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.busy.set(false);
        this.decision.set(null);
        this.toast.success(
          decision === 'approve' ? 'Driver approved.' : decision === 'reject' ? 'Driver rejected.' : 'Driver suspended. Their cars are hidden from search.',
        );
        this.cars.dashboard().subscribe({ error: () => undefined });
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(apiErrorMessage(err, 'Could not save this decision.'));
      },
    });
  }

  dialogTitle(decision: DriverDecision): string {
    if (decision === 'approve') return this.profile()?.status === DriverStatus.Suspended ? 'Lift suspension' : 'Approve driver';
    return decision === 'reject' ? 'Reject driver' : 'Suspend driver';
  }

  dialogMessage(decision: DriverDecision): string {
    switch (decision) {
      case 'approve':
        return this.addedAndPending()
          ? 'You added this driver, so no profile review is needed. Their approved cars go live in customer search straight away.'
          : 'The driver can take bookings once approved. Each of their cars still needs its own approval.';
      case 'reject':
        return 'The driver sees your reason, fixes their profile and sends it again.';
      case 'suspend':
        return 'All of this driver’s cars leave customer search at once. Existing bookings stay with you to reassign or cancel.';
    }
  }

  carBadge(status: CarStatus): string {
    return approvalBadgeClass(status, CarStatus.Approved, [CarStatus.Rejected, CarStatus.Suspended, CarStatus.Inactive]);
  }
}
