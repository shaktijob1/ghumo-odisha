import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminCarDetail, AdminDecision } from '../../../core/models/admin-car.model';
import {
  ApprovalLabels,
  CarPhotoKind,
  CarPricingStatus,
  CarStatus,
  DriverStatus,
  FuelTypeLabels,
  approvalBadgeClass,
} from '../../../core/models/car.model';
import { CarPricing, SubmitPricingRequest } from '../../../core/models/driver.model';
import { AdminCarService } from '../../../core/services/admin-car.service';
import { ToastService } from '../../../core/services/toast.service';
import { PricingEditorComponent } from '../../../shared/components/pricing-editor.component';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';
import { apiErrorMessage } from '../../../shared/utils/api-error';
import { istDateTime } from '../../../shared/utils/car-format';
import { AdminDocumentsComponent } from './admin-documents.component';
import { AuditHistoryComponent } from './audit-history.component';
import { DecisionDialogComponent } from './decision-dialog.component';
import { PricingSummaryComponent } from './pricing-summary.component';

type LoadState = 'loading' | 'ready' | 'error';
/** Car decisions plus rejecting a pending pricing proposal — all go through the same reason dialog. */
type Pending = { kind: 'car'; decision: AdminDecision } | { kind: 'pricing-reject'; pricing: CarPricing };

@Component({
  selector: 'app-admin-car-detail',
  standalone: true,
  imports: [
    DatePipe,
    RouterLink,
    ImageUrlPipe,
    StatePanelComponent,
    PricingEditorComponent,
    AdminDocumentsComponent,
    AuditHistoryComponent,
    DecisionDialogComponent,
    PricingSummaryComponent,
  ],
  template: `
    <a class="aback" routerLink="/admin/cars">← Cars</a>
    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <app-state-panel kind="error" message="Could not load this car."></app-state-panel> }
      @case ('ready') {
        @let d = detail()!;
        @let c = d.car;

        <div class="ahead">
          <div>
            <h2>{{ c.displayName }}</h2>
            <div class="sub">
              <span [class]="'badge ' + badge()">{{ awaitingReview() ? 'Waiting for review' : labels[c.status] }}</span>
              @if (c.isListed) { <span class="badge ok">Live in search</span> }
              <span class="mono">{{ c.registrationNumber }}</span> · {{ c.category }}
            </div>
          </div>
          <div class="row" style="gap:8px;flex-wrap:wrap">
            @if (canReject()) { <button class="btn sm dang" (click)="askCar('reject')">Reject</button> }
            @if (canDeactivate()) { <button class="btn sm ghost" (click)="askCar('deactivate')">Deactivate</button> }
            @if (canSuspend()) { <button class="btn sm dang" (click)="askCar('suspend')">Suspend</button> }
            @if (canApprove()) {
              <button class="btn sm" (click)="askCar('approve')">{{ c.status === CarStatus.Suspended ? 'Lift suspension' : 'Approve & publish' }}</button>
            }
          </div>
        </div>

        @if (c.statusReason && c.status !== CarStatus.Approved) {
          <div class="panel acar-reason"><b>{{ labels[c.status] }}:</b> {{ c.statusReason }}</div>
        }
        @if (d.notListedReasons.length) {
          <div class="panel acar-missing">
            <b>Not visible to customers because</b>
            <ul>@for (r of d.notListedReasons; track r) { <li>{{ r }}</li> }</ul>
            @if (d.driver.status !== DriverStatus.Approved) {
              <a [routerLink]="['/admin/drivers', d.driver.driverId]" style="font-weight:600">Open driver {{ d.driver.name }} to approve →</a>
            }
          </div>
        }
        @if (c.missingForReview.length) {
          <div class="panel acar-missing">
            <b>Car details not complete</b>
            <ul>@for (m of c.missingForReview; track m) { <li>{{ m }}</li> }</ul>
          </div>
        }

        <div class="panel" style="margin-bottom:14px">
          <h4>Photos</h4>
          <div class="sub">At least one outside and one inside photo. The first outside photo is the cover in search.</div>
          @for (group of photoGroups(); track group.kind) {
            <div class="row sp" style="margin-top:14px">
              <div class="lbl" style="margin:0">{{ group.label }} · {{ group.photos.length }}</div>
              <label class="btn sm ghost" style="cursor:pointer">
                @if (uploading() === group.kind) { <span class="spin"></span> } @else { + Add {{ group.label.toLowerCase() }} photos }
                <input type="file" accept="image/jpeg,image/png,image/webp" multiple style="display:none" [disabled]="uploading() !== null" (change)="addPhotos($event, group.kind)" />
              </label>
            </div>
            @if (group.photos.length === 0) {
              <p class="note" style="margin-top:6px">None yet.</p>
            } @else {
              <div class="acar-photos">
                @for (ph of group.photos; track ph.carPhotoId) {
                  <div class="acar-ph">
                    <a [href]="ph.imageUrl | imageUrl" target="_blank" rel="noopener"><img [src]="ph.imageUrl | imageUrl" alt="" loading="lazy" /></a>
                    <button type="button" class="pe-x" aria-label="Remove photo" [disabled]="removing() === ph.carPhotoId" (click)="removePhoto(ph.carPhotoId)">×</button>
                  </div>
                }
              </div>
            }
          }
          @if (photoError()) { <div class="errorbox">{{ photoError() }}</div> }
        </div>

        <div class="panels">
          <div class="panel">
            <h4>Car</h4>
            <div class="kv" style="margin-top:10px">
              <span class="k">Brand / model</span><b>{{ c.brand }} {{ c.modelName }}</b>
              <span class="k">Seats</span><b>{{ c.seatCapacity }}</b>
              <span class="k">Fuel</span><b>{{ fuel[c.fuelType] }}</b>
              <span class="k">AC</span><b>{{ c.hasAc ? 'Yes' : 'No' }}</b>
              <span class="k">Base city</span><b>{{ c.baseCity }}</b>
              <span class="k">Upcoming bookings</span><b>{{ d.upcomingBookings }}</b>
              <span class="k">Sent for review</span><b>{{ c.submittedForReviewAt ? when(c.submittedForReviewAt) : '—' }}</b>
              <span class="k">Approved</span><b>{{ c.approvedAt ? when(c.approvedAt) : '—' }}</b>
              <span class="k">Added</span><b>{{ c.createdAt | date: 'd MMM y' }}</b>
            </div>
            @if (c.description) {
              <div class="hr"></div>
              <div class="lbl">Description</div>
              <p class="note">{{ c.description }}</p>
            }
          </div>
          <div class="panel">
            <h4>Driver</h4>
            <div class="kv" style="margin-top:10px">
              <span class="k">Name</span><a [routerLink]="['/admin/drivers', d.driver.driverId]"><b>{{ d.driver.name }}</b></a>
              <span class="k">WhatsApp</span><b>{{ d.driver.phoneNumber ? '+91 ' + d.driver.phoneNumber : '—' }}</b>
              <span class="k">Status</span><span><span [class]="'badge ' + driverBadge()">{{ driverLabels[d.driver.status] }}</span></span>
            </div>
            <div class="hr"></div>
            <h4>Documents</h4>
            <div class="sub">The driver's licence, and this car's RC and insurance</div>
            <div style="margin-top:8px"><app-admin-documents [documents]="d.documents" [carId]="c.carId" emptyText="No documents uploaded."></app-admin-documents></div>
          </div>
        </div>

        <div class="panel" style="margin-bottom:14px">
          <div class="row sp" style="align-items:flex-start;gap:12px;flex-wrap:wrap">
            <div>
              <h4>Pricing</h4>
              <div class="sub">Pricing you set here is live straight away, with no approval step. The driver's version stays in the history.</div>
            </div>
            @if (!editingPricing()) {
              <button class="btn sm ghost" (click)="editingPricing.set(true); pricingError.set(null)">{{ c.activePricing ? 'Change pricing' : 'Set pricing' }}</button>
            }
          </div>

          @if (editingPricing()) {
            <div class="acar-editor">
              <app-pricing-editor
                [initial]="c.pendingPricing ?? c.activePricing"
                submitLabel="Save and apply now"
                [busy]="pricingBusy()"
                [error]="pricingError()"
                (submitted)="savePricing($event)"
              ></app-pricing-editor>
              <button class="linkbtn" style="padding:10px 0 0" (click)="editingPricing.set(false)">Cancel</button>
            </div>
          } @else {
            <div class="acar-pricing">
              <div>
                <div class="lbl">Current (live)</div>
                @if (c.activePricing; as ap) { <app-pricing-summary [pricing]="ap"></app-pricing-summary> } @else { <p class="note">No approved pricing yet.</p> }
              </div>
              @if (c.pendingPricing; as pp) {
                <div class="acar-proposal">
                  <div class="lbl">Proposed by {{ pp.submittedByRole.toLowerCase() }} · {{ when(pp.submittedAt) }}</div>
                  <app-pricing-summary [pricing]="pp"></app-pricing-summary>
                  <div class="row" style="gap:8px;margin-top:12px">
                    <button class="btn sm dang" (click)="askPricingReject(pp)">Reject</button>
                    <button class="btn sm" [disabled]="pricingBusy()" (click)="approvePricing(pp)">
                      @if (pricingBusy()) { <span class="spin"></span> } @else { Approve pricing }
                    </button>
                  </div>
                  @if (pricingError()) { <div class="errorbox">{{ pricingError() }}</div> }
                </div>
              }
            </div>
          }

          @if (d.pricingHistory.length) {
            <div class="hr"></div>
            <div class="lbl">Pricing history</div>
            <div class="tblwrap" style="margin-top:8px">
              <table class="tbl">
                <tr><th>Submitted</th><th>By</th><th>Per km</th><th>Night halt</th><th>Base fares</th><th>Status</th><th>Review note</th></tr>
                @for (h of d.pricingHistory; track h.carPricingId) {
                  <tr>
                    <td>{{ when(h.submittedAt) }}</td>
                    <td>{{ h.submittedByRole }}</td>
                    <td>₹{{ h.pricePerKm }}</td>
                    <td>₹{{ h.nightHaltPrice }}</td>
                    <td class="note">{{ tierText(h) }}</td>
                    <td><span [class]="'badge ' + pricingBadge(h.status)">{{ pricingLabels[h.status] }}</span></td>
                    <td class="note">{{ h.reviewNote || '—' }}</td>
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

    @if (pending(); as p) {
      <app-decision-dialog
        [title]="dialogTitle(p)"
        [message]="dialogMessage(p)"
        [confirmLabel]="dialogTitle(p)"
        [reasonLabel]="p.kind === 'car' && p.decision === 'approve' ? 'Note' : 'Reason (the driver will see this)'"
        [reasonRequired]="!(p.kind === 'car' && p.decision === 'approve')"
        [danger]="!(p.kind === 'car' && p.decision === 'approve')"
        [busy]="busy()"
        [error]="error()"
        (confirmed)="confirm(p, $event)"
        (closed)="pending.set(null)"
      ></app-decision-dialog>
    }
  `,
})
export class AdminCarDetailComponent implements OnInit {
  private readonly cars = inject(AdminCarService);
  private readonly route = inject(ActivatedRoute);
  private readonly toast = inject(ToastService);

  readonly labels = ApprovalLabels.car;
  readonly driverLabels = ApprovalLabels.driver;
  readonly pricingLabels = ApprovalLabels.pricing;
  readonly fuel = FuelTypeLabels;
  readonly CarStatus = CarStatus;
  readonly DriverStatus = DriverStatus;
  readonly when = istDateTime;

  readonly state = signal<LoadState>('loading');
  readonly detail = signal<AdminCarDetail | null>(null);
  readonly pending = signal<Pending | null>(null);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly editingPricing = signal(false);
  readonly pricingBusy = signal(false);
  readonly pricingError = signal<string | null>(null);
  readonly uploading = signal<CarPhotoKind | null>(null);
  readonly removing = signal<number | null>(null);
  readonly photoError = signal<string | null>(null);

  private readonly car = computed(() => this.detail()?.car ?? null);
  readonly awaitingReview = computed(() => {
    const c = this.car();
    return !!c && c.status === CarStatus.Pending && !!c.submittedForReviewAt;
  });
  // Mirrors the server's rules (AdminCarService) so only buttons that can succeed are shown.
  readonly canApprove = computed(() => {
    const c = this.car();
    if (!c || c.status === CarStatus.Approved) return false;
    return c.status === CarStatus.Suspended || c.status === CarStatus.Inactive || !!c.submittedForReviewAt;
  });
  readonly canReject = computed(() => this.awaitingReview());
  readonly canSuspend = computed(() => {
    const s = this.car()?.status;
    return s === CarStatus.Approved || s === CarStatus.Inactive;
  });
  readonly canDeactivate = computed(() => this.car()?.status === CarStatus.Approved);
  readonly badge = computed(() =>
    approvalBadgeClass(this.car()?.status ?? CarStatus.Pending, CarStatus.Approved, [CarStatus.Rejected, CarStatus.Suspended, CarStatus.Inactive]),
  );
  readonly driverBadge = computed(() =>
    approvalBadgeClass(this.detail()?.driver.status ?? DriverStatus.Pending, DriverStatus.Approved, [DriverStatus.Rejected, DriverStatus.Suspended]),
  );
  readonly photoGroups = computed(() => {
    const photos = [...(this.car()?.photos ?? [])].sort((a, b) => a.displayOrder - b.displayOrder);
    return [
      { kind: CarPhotoKind.Exterior, label: 'Outside', photos: photos.filter((p) => p.kind === CarPhotoKind.Exterior) },
      { kind: CarPhotoKind.Interior, label: 'Inside', photos: photos.filter((p) => p.kind === CarPhotoKind.Interior) },
    ];
  });

  private get id(): number {
    return Number(this.route.snapshot.paramMap.get('id'));
  }

  ngOnInit(): void {
    this.cars.car(this.id).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  askCar(decision: AdminDecision): void {
    this.error.set(null);
    this.pending.set({ kind: 'car', decision });
  }

  askPricingReject(pricing: CarPricing): void {
    this.error.set(null);
    this.pending.set({ kind: 'pricing-reject', pricing });
  }

  confirm(p: Pending, reason: string | null): void {
    this.busy.set(true);
    this.error.set(null);
    const call =
      p.kind === 'car' ? this.cars.decideCar(this.id, p.decision, reason) : this.cars.rejectPricing(p.pricing.carPricingId, reason ?? '');
    call.subscribe({
      next: (d) => {
        this.done(d);
        this.busy.set(false);
        this.pending.set(null);
        this.toast.success(p.kind === 'pricing-reject' ? 'Pricing rejected.' : SUCCESS[p.decision]);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(apiErrorMessage(err, 'Could not save this decision.'));
      },
    });
  }

  approvePricing(pricing: CarPricing): void {
    this.pricingBusy.set(true);
    this.pricingError.set(null);
    this.cars.approvePricing(pricing.carPricingId).subscribe({
      next: (d) => {
        this.done(d);
        this.pricingBusy.set(false);
        this.toast.success('Pricing approved. New bookings use it from now on.');
      },
      error: (err) => {
        this.pricingBusy.set(false);
        this.pricingError.set(apiErrorMessage(err, 'Could not approve this pricing.'));
      },
    });
  }

  savePricing(request: SubmitPricingRequest): void {
    this.pricingBusy.set(true);
    this.pricingError.set(null);
    this.cars.setPricing(this.id, request).subscribe({
      next: (d) => {
        this.done(d);
        this.pricingBusy.set(false);
        this.editingPricing.set(false);
        this.toast.success('Pricing saved and live.');
      },
      error: (err) => {
        this.pricingBusy.set(false);
        this.pricingError.set(apiErrorMessage(err, 'Could not save this pricing.'));
      },
    });
  }

  /** Uploads the chosen photos one by one (each is shrunk in the browser first). */
  addPhotos(event: Event, kind: CarPhotoKind): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    if (!files.length) return;
    this.uploading.set(kind);
    this.photoError.set(null);
    const next = (i: number): void => {
      if (i >= files.length) {
        this.uploading.set(null);
        return;
      }
      this.cars.addPhoto(this.id, files[i], kind).subscribe({
        next: (d) => {
          this.detail.set(d);
          next(i + 1);
        },
        error: (err) => {
          this.uploading.set(null);
          this.photoError.set(apiErrorMessage(err, 'Could not upload this photo.'));
        },
      });
    };
    next(0);
  }

  removePhoto(photoId: number): void {
    this.removing.set(photoId);
    this.photoError.set(null);
    this.cars.deletePhoto(this.id, photoId).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.removing.set(null);
      },
      error: (err) => {
        this.removing.set(null);
        this.photoError.set(apiErrorMessage(err, 'Could not remove this photo.'));
      },
    });
  }

  dialogTitle(p: Pending): string {
    if (p.kind === 'pricing-reject') return 'Reject pricing';
    switch (p.decision) {
      case 'approve':
        return this.car()?.status === CarStatus.Suspended ? 'Lift suspension' : 'Approve & publish';
      case 'reject':
        return 'Reject car';
      case 'suspend':
        return 'Suspend car';
      case 'deactivate':
        return 'Deactivate car';
    }
  }

  dialogMessage(p: Pending): string {
    if (p.kind === 'pricing-reject') return 'The current pricing stays live. The driver sees your reason and can propose again.';
    const upcoming = this.detail()?.upcomingBookings ?? 0;
    const bookingsNote = upcoming ? ` It has ${upcoming} upcoming booking(s); they are not cancelled automatically.` : '';
    switch (p.decision) {
      case 'approve':
        return 'The car appears in customer search once its driver and pricing are approved too.';
      case 'reject':
        return 'The driver sees your reason, fixes the car and sends it again.';
      case 'suspend':
        return 'The car leaves customer search, and only an admin can lift the suspension.' + bookingsNote;
      case 'deactivate':
        return 'The car leaves customer search. The driver can send it for review again.' + bookingsNote;
    }
  }

  tierText(p: CarPricing): string {
    const sorted = [...p.tiers].sort((a, b) => (a.upToKm ?? Infinity) - (b.upToKm ?? Infinity));
    return sorted.map((t) => (t.upToKm === null ? `rest ₹${t.baseFare}` : `≤${t.upToKm} km ₹${t.baseFare}`)).join(' · ');
  }

  pricingBadge(status: CarPricingStatus): string {
    if (status === CarPricingStatus.Approved) return 'ok';
    if (status === CarPricingStatus.Pending) return 'wait';
    if (status === CarPricingStatus.Rejected) return 'bad';
    return 'info';
  }

  private done(d: AdminCarDetail): void {
    this.detail.set(d);
    this.cars.dashboard().subscribe({ error: () => undefined });
  }
}

const SUCCESS: Record<AdminDecision, string> = {
  approve: 'Car approved.',
  reject: 'Car rejected.',
  suspend: 'Car suspended and hidden from search.',
  deactivate: 'Car deactivated and hidden from search.',
};
