import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { concatMap, from, toArray } from 'rxjs';
import {
  ApprovalLabels,
  CarPhotoKind,
  CarPricingStatus,
  CarStatus,
  DriverDocumentType,
  DriverDocumentTypeLabels,
  FuelType,
  FuelTypeLabels,
  approvalBadgeClass,
} from '../../core/models/car.model';
import { CarPricing, DriverCar, DriverDocument, SubmitPricingRequest } from '../../core/models/driver.model';
import { CarService } from '../../core/services/car.service';
import { DriverService } from '../../core/services/driver.service';
import { ToastService } from '../../core/services/toast.service';
import { PricingEditorComponent } from '../../shared/components/pricing-editor.component';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { apiErrorMessage } from '../../shared/utils/api-error';
import { istDate } from '../../shared/utils/car-format';

interface PendingUpload {
  key: number;
  kind: CarPhotoKind;
  preview: string;
}

/**
 * /driver/cars/new and /driver/cars/:id — register or edit a car: details, outside/inside photos
 * (previewed, shrunk before upload), car documents, pricing proposal and "send for approval".
 */
@Component({
  selector: 'app-driver-car-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ImageUrlPipe, StatePanelComponent, PricingEditorComponent],
  template: `
    <a class="cz-back" routerLink="/driver/cars">← Your cars</a>
    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <div class="card pad cz-empty"><p>{{ error() }}</p><a class="btn ghost sm" routerLink="/driver/cars">Back</a></div> }
      @default {
        <div class="cz-cardhead" style="margin-bottom:14px">
          <h1 class="dv-h1" style="margin:0">{{ car() ? car()!.displayName : 'Add your car' }}</h1>
          @if (car(); as c) {
            @if (c.isListed) { <span class="badge ok">Live — customers can book</span> } @else { <span class="badge" [class]="badge()">{{ statusLabel() }}</span> }
          }
        </div>

        @if (car(); as c) {
          @if (c.statusReason && c.status !== Status.Approved) {
            <div class="card pad cz-alert" style="margin-bottom:14px"><b>Note from Ghumo Odisha</b><span>{{ c.statusReason }}</span></div>
          }
        }

        <!-- 1. Details -->
        <section class="card pad">
          <h3 class="cz-h3">1. Car details</h3>
          <form (ngSubmit)="saveDetails()">
            <div class="f2 dv-f2">
              <div class="fld"><label class="lbl" for="c-brand">Brand</label><input id="c-brand" class="inp" name="brand" [(ngModel)]="form.brand" placeholder="e.g. Toyota" maxlength="60" /></div>
              <div class="fld"><label class="lbl" for="c-model">Model</label><input id="c-model" class="inp" name="model" [(ngModel)]="form.modelName" placeholder="e.g. Innova Crysta" maxlength="100" /></div>
            </div>
            <div class="f2 dv-f2">
              <div class="fld"><label class="lbl" for="c-reg">Registration number</label><input id="c-reg" class="inp" name="reg" [(ngModel)]="form.registrationNumber" placeholder="e.g. OD02AB1234" maxlength="20" style="text-transform:uppercase" /></div>
              <div class="fld"><label class="lbl" for="c-city">City the car is based in</label><input id="c-city" class="inp" name="city" [(ngModel)]="form.baseCity" placeholder="e.g. Bhubaneswar" maxlength="100" /></div>
            </div>
            <div class="f2 dv-f2">
              <div class="fld">
                <label class="lbl" for="c-seats">Seats</label>
                <select id="c-seats" class="inp" name="seats" [(ngModel)]="form.seatCapacity">
                  @for (s of seatOptions(); track s) { <option [ngValue]="s">{{ s }} Seater {{ s >= 13 ? 'Tempo Traveller' : 'Car' }}</option> }
                </select>
              </div>
              <div class="fld">
                <label class="lbl" for="c-fuel">Fuel</label>
                <select id="c-fuel" class="inp" name="fuel" [(ngModel)]="form.fuelType">
                  @for (f of fuels; track f.value) { <option [ngValue]="f.value">{{ f.label }}</option> }
                </select>
              </div>
            </div>
            <div class="fld">
              <span class="lbl">Air conditioning</span>
              <div class="dv-seg" role="radiogroup">
                <button type="button" role="radio" [class.on]="form.hasAc" [attr.aria-checked]="form.hasAc" (click)="form.hasAc = true">AC</button>
                <button type="button" role="radio" [class.on]="!form.hasAc" [attr.aria-checked]="!form.hasAc" (click)="form.hasAc = false">Non-AC</button>
              </div>
            </div>
            <div class="fld"><label class="lbl" for="c-desc">About the car <span class="mut">(optional)</span></label>
              <textarea id="c-desc" class="inp" rows="3" name="desc" [(ngModel)]="form.description" maxlength="2000" placeholder="Condition, comfort, luggage space, music system…"></textarea></div>
            @if (car()?.status === Status.Approved) {
              <p class="note" style="margin-bottom:10px">Changing brand, model, number, seats, fuel, AC or city sends the car for approval again.</p>
            }
            @if (detailsError()) { <div class="errorbox">{{ detailsError() }}</div> }
            <button type="submit" class="btn" [disabled]="savingDetails()">
              @if (savingDetails()) { <span class="spin"></span> } @else { {{ car() ? 'Save details' : 'Save and continue' }} }
            </button>
          </form>
        </section>

        @if (car(); as c) {
          <!-- 2. Photos -->
          <section class="card pad">
            <h3 class="cz-h3">2. Photos</h3>
            <p class="note" style="margin-bottom:12px">At least one outside and one inside photo. Clear, daylight photos get more bookings.</p>
            @for (k of kinds; track k.kind) {
              <div class="dv-photogroup">
                <div class="cz-cardhead"><b>{{ k.label }} ({{ photosOf(k.kind).length }})</b>
                  <label class="btn ghost sm dv-file">+ Add photos
                    <input type="file" accept="image/jpeg,image/png,image/webp" multiple (change)="addPhotos($event, k.kind)" />
                  </label>
                </div>
                <div class="dv-photos">
                  @for (p of photosOf(k.kind); track p.carPhotoId) {
                    <div class="dv-ph">
                      <img [src]="p.imageUrl | imageUrl" alt="" />
                      <button type="button" class="pe-x" (click)="deletePhoto(p.carPhotoId)" aria-label="Remove photo">×</button>
                    </div>
                  }
                  @for (u of uploadsOf(k.kind); track u.key) {
                    <div class="dv-ph uploading"><img [src]="u.preview" alt="" /><span class="spin"></span></div>
                  }
                  @if (photosOf(k.kind).length === 0 && uploadsOf(k.kind).length === 0) { <p class="mut">No {{ k.label.toLowerCase() }} yet.</p> }
                </div>
              </div>
            }
            @if (photoError()) { <div class="errorbox">{{ photoError() }}</div> }
          </section>

          <!-- 3. Documents -->
          <section class="card pad">
            <h3 class="cz-h3">3. Car documents</h3>
            <p class="note" style="margin-bottom:12px">RC, insurance and permit help us approve faster. Only you and Ghumo Odisha see them.</p>
            @for (d of carDocs(); track d.driverDocumentId) {
              <div class="dv-doc"><div><b>{{ docLabel(d.documentType) }}</b><span class="mut">Uploaded {{ date(d.createdAt) }}</span></div>
                <button type="button" class="btn ghost sm" (click)="viewDoc(d)">View</button></div>
            }
            <div class="row wrap" style="margin-top:10px">
              <select class="inp" style="flex:1;min-width:180px" [(ngModel)]="docType" name="docType">
                @for (t of docTypes; track t.value) { <option [ngValue]="t.value">{{ t.label }}</option> }
              </select>
              <label class="btn sm dv-file">@if (docBusy()) { <span class="spin"></span> } @else { Upload }
                <input type="file" accept="image/jpeg,image/png,image/webp,application/pdf" (change)="uploadDoc($event)" [disabled]="docBusy()" /></label>
            </div>
          </section>

          <!-- 4. Pricing -->
          <section class="card pad">
            <h3 class="cz-h3">4. Pricing</h3>
            @if (c.activePricing; as a) {
              <div class="dv-price-now">
                <span class="badge ok">Live pricing</span>
                <span>₹{{ a.pricePerKm | number: '1.0-2' }}/km · night halt ₹{{ a.nightHaltPrice | number: '1.0-0' }} · base {{ tierSummary(a) }}</span>
              </div>
            }
            @if (c.pendingPricing; as p) {
              <div class="dv-price-now">
                <span class="badge wait">Waiting for approval</span>
                <span>₹{{ p.pricePerKm | number: '1.0-2' }}/km · night halt ₹{{ p.nightHaltPrice | number: '1.0-0' }} · base {{ tierSummary(p) }}</span>
              </div>
            }
            @if (c.rejectedPricing; as r) {
              <div class="card pad cz-alert" style="margin-bottom:12px"><b>Your last pricing wasn't approved</b><span>{{ r.reviewNote }}</span></div>
            }
            <p class="note" style="margin-bottom:12px">New prices go to Ghumo Odisha for approval. @if (c.activePricing) { Your live pricing stays active until then. }</p>
            <app-pricing-editor [initial]="c.pendingPricing ?? c.activePricing ?? c.rejectedPricing" [defaultNightHalt]="defaultNightHalt()"
              [busy]="pricingBusy()" [error]="pricingError()" (submitted)="submitPricing($event)"></app-pricing-editor>
            <button type="button" class="linkbtn" style="padding:10px 0 0" (click)="toggleHistory()">{{ history() ? 'Hide' : 'Show' }} pricing history</button>
            @if (history(); as h) {
              <table class="cz-table" style="margin-top:8px">
                <tbody>
                  @for (p of h; track p.carPricingId) {
                    <tr><td>{{ date(p.submittedAt) }} · ₹{{ p.pricePerKm | number: '1.0-2' }}/km · base {{ tierSummary(p) }}</td><td>{{ pricingLabel(p.status) }}</td></tr>
                  }
                </tbody>
              </table>
            }
          </section>

          <!-- 5. Approval -->
          <section class="card pad">
            <h3 class="cz-h3">5. Approval</h3>
            @if (c.status === Status.Approved) {
              <p class="mut">Approved @if (!c.isListed) { — it shows to customers once your profile and pricing are approved too. } @else { and live. }</p>
            } @else if (c.status === Status.Suspended) {
              <p class="mut">This car is suspended. Please contact Ghumo Odisha.</p>
            } @else if (c.status === Status.Pending && c.submittedForReviewAt) {
              <p class="mut">With our team for review.</p>
            } @else {
              @if (c.missingForReview.length) {
                <ul class="dv-todo">@for (m of c.missingForReview; track m) { <li>{{ m }}</li> }</ul>
              } @else {
                <p class="mut">Everything's ready. Send the car to our team.</p>
              }
              @if (submitError()) { <div class="errorbox" style="margin-top:10px">{{ submitError() }}</div> }
              <button type="button" class="btn" style="margin-top:12px" [disabled]="c.missingForReview.length > 0 || submitting()" (click)="submitCar()">
                @if (submitting()) { <span class="spin"></span> } @else { Send car for approval }
              </button>
            }
            @if (c.status !== Status.Inactive && c.status !== Status.Suspended) {
              <div class="hr"></div>
              <button type="button" class="btn dang sm" (click)="deactivate()">Take this car off the site</button>
            }
          </section>
        }
      }
    }
  `,
})
export class DriverCarFormComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly driverService = inject(DriverService);
  private readonly carService = inject(CarService);
  private readonly toast = inject(ToastService);

  readonly Status = CarStatus;
  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly error = signal('');
  readonly car = signal<DriverCar | null>(null);
  /** Seat categories come from the API settings, not from here. */
  readonly seatOptions = signal<number[]>([]);
  readonly defaultNightHalt = signal(400);

  form = { brand: '', modelName: '', registrationNumber: '', fuelType: FuelType.Diesel, seatCapacity: 5, hasAc: true, description: '', baseCity: '' };
  readonly fuels = Object.values(FuelType)
    .filter((v): v is FuelType => typeof v === 'number')
    .map((value) => ({ value, label: FuelTypeLabels[value] }));
  readonly kinds = [
    { kind: CarPhotoKind.Exterior, label: 'Outside photos' },
    { kind: CarPhotoKind.Interior, label: 'Inside photos' },
  ];
  readonly docTypes = [DriverDocumentType.RegistrationCertificate, DriverDocumentType.Insurance, DriverDocumentType.Permit, DriverDocumentType.Other].map(
    (value) => ({ value, label: DriverDocumentTypeLabels[value] }),
  );
  docType = DriverDocumentType.RegistrationCertificate;

  readonly savingDetails = signal(false);
  readonly detailsError = signal<string | null>(null);
  readonly uploads = signal<PendingUpload[]>([]);
  readonly photoError = signal<string | null>(null);
  readonly docs = signal<DriverDocument[]>([]);
  readonly docBusy = signal(false);
  readonly pricingBusy = signal(false);
  readonly pricingError = signal<string | null>(null);
  readonly history = signal<CarPricing[] | null>(null);
  readonly submitting = signal(false);
  readonly submitError = signal<string | null>(null);

  readonly carDocs = computed(() => this.docs().filter((d) => d.carId === this.car()?.carId));
  readonly statusLabel = computed(() => {
    const c = this.car();
    if (!c) return '';
    return c.status === CarStatus.Pending && !c.submittedForReviewAt ? 'Not submitted' : ApprovalLabels.car[c.status];
  });
  readonly badge = computed(() => approvalBadgeClass(this.car()?.status ?? 0, CarStatus.Approved, [CarStatus.Rejected, CarStatus.Suspended, CarStatus.Inactive]));
  readonly date = istDate;
  private uploadKey = 0;

  ngOnInit(): void {
    this.carService.settings().subscribe({
      next: (s) => {
        this.seatOptions.set(s.seatCapacities);
        if (!s.seatCapacities.includes(this.form.seatCapacity)) this.form.seatCapacity = s.seatCapacities[0];
        this.defaultNightHalt.set(s.defaultNightHaltPrice);
      },
      error: () => undefined,
    });
    const id = this.route.snapshot.paramMap.get('id');
    if (!id || id === 'new') {
      this.state.set('ready');
      return;
    }
    this.driverService.car(Number(id)).subscribe({
      next: (c) => {
        this.apply(c);
        this.loadDocs();
        this.state.set('ready');
      },
      error: (e: unknown) => {
        this.error.set(apiErrorMessage(e, 'Could not load this car.'));
        this.state.set('error');
      },
    });
  }

  saveDetails(): void {
    this.savingDetails.set(true);
    this.detailsError.set(null);
    const request = { ...this.form, description: this.form.description.trim() || null };
    const existing = this.car();
    const call = existing ? this.driverService.updateCar(existing.carId, request) : this.driverService.createCar(request);
    call.subscribe({
      next: (c) => {
        this.savingDetails.set(false);
        this.apply(c);
        if (!existing) {
          this.toast.success('Car added. Now add photos and pricing.');
          this.router.navigate(['/driver/cars', c.carId], { replaceUrl: true });
        } else {
          this.toast.success('Details saved.');
        }
      },
      error: (e: unknown) => {
        this.savingDetails.set(false);
        this.detailsError.set(apiErrorMessage(e));
      },
    });
  }

  photosOf(kind: CarPhotoKind) {
    return (this.car()?.photos ?? []).filter((p) => p.kind === kind);
  }

  uploadsOf(kind: CarPhotoKind): PendingUpload[] {
    return this.uploads().filter((u) => u.kind === kind);
  }

  /** Uploads one at a time with a preview each, so a slow connection shows progress. */
  addPhotos(event: Event, kind: CarPhotoKind): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    const car = this.car();
    if (!car || files.length === 0) return;
    this.photoError.set(null);

    const pending = files.map((f) => ({ file: f, upload: { key: ++this.uploadKey, kind, preview: URL.createObjectURL(f) } }));
    this.uploads.update((u) => [...u, ...pending.map((p) => p.upload)]);

    from(pending)
      .pipe(
        concatMap(({ file, upload }) => {
          const done = () => this.uploads.update((u) => u.filter((x) => x.key !== upload.key));
          return new Promise<void>((resolve) =>
            this.driverService.addCarPhoto(car.carId, kind, file).subscribe({
              next: (c) => {
                this.apply(c, false);
                done();
                resolve();
              },
              error: (e: unknown) => {
                this.photoError.set(apiErrorMessage(e, 'A photo could not be uploaded.'));
                done();
                resolve();
              },
            }),
          );
        }),
        toArray(),
      )
      .subscribe();
  }

  deletePhoto(photoId: number): void {
    const car = this.car();
    if (!car) return;
    this.photoError.set(null);
    this.driverService.deleteCarPhoto(car.carId, photoId).subscribe({
      next: (c) => this.apply(c, false),
      error: (e: unknown) => this.photoError.set(apiErrorMessage(e)),
    });
  }

  uploadDoc(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    const car = this.car();
    if (!file || !car) return;
    this.docBusy.set(true);
    this.driverService.uploadDocument(file, this.docType, car.carId).subscribe({
      next: () => {
        this.docBusy.set(false);
        this.toast.success('Document uploaded.');
        this.loadDocs();
      },
      error: (e: unknown) => {
        this.docBusy.set(false);
        this.toast.error(apiErrorMessage(e, 'Could not upload the document.'));
      },
    });
  }

  viewDoc(d: DriverDocument): void {
    const tab = window.open('', '_blank');
    this.driverService.documentFile(d.driverDocumentId).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        if (tab) tab.location.href = url;
        else window.location.href = url;
      },
      error: (e: unknown) => {
        tab?.close();
        this.toast.error(apiErrorMessage(e, 'Could not open the document.'));
      },
    });
  }

  submitPricing(request: SubmitPricingRequest): void {
    const car = this.car();
    if (!car) return;
    this.pricingBusy.set(true);
    this.pricingError.set(null);
    this.driverService.submitPricing(car.carId, request).subscribe({
      next: (c) => {
        this.pricingBusy.set(false);
        this.apply(c, false);
        this.history.set(null);
        this.toast.success('Pricing sent for approval.');
      },
      error: (e: unknown) => {
        this.pricingBusy.set(false);
        this.pricingError.set(apiErrorMessage(e));
      },
    });
  }

  toggleHistory(): void {
    const car = this.car();
    if (this.history() || !car) {
      this.history.set(null);
      return;
    }
    this.driverService.pricingHistory(car.carId).subscribe({ next: (h) => this.history.set(h), error: (e: unknown) => this.toast.error(apiErrorMessage(e)) });
  }

  submitCar(): void {
    const car = this.car();
    if (!car) return;
    this.submitting.set(true);
    this.submitError.set(null);
    this.driverService.submitCar(car.carId).subscribe({
      next: (c) => {
        this.submitting.set(false);
        this.apply(c, false);
        this.toast.success('Car sent for approval.');
      },
      error: (e: unknown) => {
        this.submitting.set(false);
        this.submitError.set(apiErrorMessage(e));
      },
    });
  }

  deactivate(): void {
    const car = this.car();
    if (!car || !confirm('Take this car off the site? Customers won’t be able to book it until you send it for approval again.')) return;
    this.driverService.deactivateCar(car.carId).subscribe({
      next: (c) => {
        this.apply(c, false);
        this.toast.success('Car taken off the site.');
      },
      error: (e: unknown) => this.toast.error(apiErrorMessage(e)),
    });
  }

  tierSummary(p: CarPricing): string {
    const sorted = [...p.tiers].sort((a, b) => (a.upToKm ?? Infinity) - (b.upToKm ?? Infinity));
    return sorted.map((t) => `${t.upToKm === null ? 'above' : '≤' + t.upToKm + ' km'} ₹${t.baseFare.toLocaleString('en-IN')}`).join(', ');
  }

  pricingLabel(s: CarPricingStatus): string {
    return ApprovalLabels.pricing[s];
  }

  docLabel(t: DriverDocumentType): string {
    return DriverDocumentTypeLabels[t];
  }

  private loadDocs(): void {
    this.driverService.profile().subscribe({ next: (p) => this.docs.set(p.documents), error: () => undefined });
  }

  /** Shows the latest car. Only a load or a details save refills the details form — photo, pricing and
   *  status updates must not wipe details the driver is still editing. */
  private apply(c: DriverCar, refillForm = true): void {
    this.car.set(c);
    if (!refillForm) return;
    this.form = {
      brand: c.brand,
      modelName: c.modelName,
      registrationNumber: c.registrationNumber,
      fuelType: c.fuelType,
      seatCapacity: c.seatCapacity,
      hasAc: c.hasAc,
      description: c.description ?? '',
      baseCity: c.baseCity,
    };
  }
}
