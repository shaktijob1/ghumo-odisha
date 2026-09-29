import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, Subject, debounceTime, of, switchMap } from 'rxjs';
import { AdminCarFilter, AdminDriverListItem } from '../../../core/models/admin-car.model';
import { ApprovalLabels, DriverStatus, FuelType, FuelTypeLabels } from '../../../core/models/car.model';
import { SaveCarRequest } from '../../../core/models/driver.model';
import { AdminCarService } from '../../../core/services/admin-car.service';
import { CarService } from '../../../core/services/car.service';
import { ToastService } from '../../../core/services/toast.service';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';
import { apiErrorMessage } from '../../../shared/utils/api-error';

type DriverMode = 'existing' | 'new';

/**
 * Admin adds a car: pick the owner-driver (or add a new one), enter the car details, then continue to
 * the car's page for photos and pricing, where "Approve & publish" puts it live.
 */
@Component({
  selector: 'app-admin-car-new',
  standalone: true,
  imports: [FormsModule, RouterLink, ImageUrlPipe],
  template: `
    <a class="aback" routerLink="/admin/cars">← Cars</a>
    <div class="ahead">
      <div>
        <h2>Add car</h2>
        <div class="sub">Every car belongs to an owner-driver: they get its bookings, drive the trips and collect the balance.</div>
      </div>
    </div>

    <form class="acar-new" (ngSubmit)="save()">
      <div class="panel">
        <h4>1. Driver</h4>
        <div class="daytabs" style="margin:12px 0">
          <button type="button" class="daytab" [class.on]="mode() === 'existing'" (click)="mode.set('existing')">Existing driver</button>
          <button type="button" class="daytab" [class.on]="mode() === 'new'" (click)="setNew()">New driver</button>
        </div>

        @if (mode() === 'existing') {
          @if (chosen(); as d) {
            <div class="row sp acar-chosen">
              <div class="row" style="gap:10px">
                @if (d.profilePhotoUrl) { <img class="acar-av" [src]="d.profilePhotoUrl | imageUrl" alt="" /> } @else { <span class="acar-av acar-av-empty">{{ d.name.charAt(0) }}</span> }
                <div>
                  <b>{{ d.name }}</b>
                  <div class="note">{{ d.phoneNumber ? '+91 ' + d.phoneNumber : 'No WhatsApp number' }}{{ d.city ? ' · ' + d.city : '' }} · {{ driverLabels[d.status] }}</div>
                </div>
              </div>
              <button type="button" class="linkbtn" (click)="chosen.set(null)">Change</button>
            </div>
            @if (d.status === DriverStatus.Suspended) {
              <div class="errorbox">This driver is suspended. Lift the suspension before adding a car.</div>
            } @else if (d.status !== DriverStatus.Approved) {
              <p class="note" style="margin-top:8px">The car can be published now, but customers only see it once this driver is approved.</p>
            }
          } @else {
            <div class="fld">
              <label class="lbl" for="an-search">Search by name, WhatsApp number or licence</label>
              <input id="an-search" class="inp" autocomplete="off" placeholder="e.g. Ramesh or 94370…" [ngModel]="search" (ngModelChange)="onSearch($event)" name="search" />
            </div>
            @if (searching()) {
              <p class="note">Searching…</p>
            } @else if (results().length) {
              <div class="acar-results">
                @for (d of results(); track d.driverId) {
                  <button type="button" class="row sp" (click)="chosen.set(d)">
                    <span><b>{{ d.name }}</b> <span class="note">{{ d.phoneNumber ? '+91 ' + d.phoneNumber : '' }}{{ d.city ? ' · ' + d.city : '' }}</span></span>
                    <span class="note">{{ driverLabels[d.status] }} · {{ d.carCount }} car(s)</span>
                  </button>
                }
              </div>
            } @else if (search.trim()) {
              <p class="note">No driver found. <button type="button" class="linkbtn" style="padding:0" (click)="setNew()">Add them as a new driver</button></p>
            }
          }
        } @else {
          <div class="f2">
            <div class="fld">
              <label class="lbl" for="an-name">Full name</label>
              <input id="an-name" class="inp" maxlength="100" name="dName" [(ngModel)]="newDriver.name" />
            </div>
            <div class="fld">
              <label class="lbl" for="an-phone">WhatsApp number</label>
              <div class="numprefix inp">
                <span class="numprefix-code">+91</span>
                <input id="an-phone" inputmode="numeric" maxlength="10" name="dPhone" [(ngModel)]="newDriver.phoneNumber" />
              </div>
            </div>
            <div class="fld">
              <label class="lbl" for="an-city">City (optional)</label>
              <input id="an-city" class="inp" maxlength="100" name="dCity" [(ngModel)]="newDriver.city" />
            </div>
          </div>
          <p class="note">You can approve this driver straight away from their driver page; no profile review needed. They can sign in later with this WhatsApp number (OTP) to see their bookings.</p>
        }
      </div>

      <div class="panel">
        <h4>2. Car details</h4>
        <div class="f2" style="margin-top:12px">
          <div class="fld">
            <label class="lbl" for="ac-brand">Brand</label>
            <input id="ac-brand" class="inp" maxlength="60" placeholder="e.g. Toyota" name="brand" [(ngModel)]="car.brand" />
          </div>
          <div class="fld">
            <label class="lbl" for="ac-model">Model</label>
            <input id="ac-model" class="inp" maxlength="100" placeholder="e.g. Innova Crysta" name="model" [(ngModel)]="car.modelName" />
          </div>
          <div class="fld">
            <label class="lbl" for="ac-reg">Registration number</label>
            <input id="ac-reg" class="inp" maxlength="20" placeholder="e.g. OD02AB1234" name="reg" [(ngModel)]="car.registrationNumber" />
          </div>
          <div class="fld">
            <label class="lbl" for="ac-seats">Seats</label>
            <select id="ac-seats" class="inp" name="seats" [(ngModel)]="car.seatCapacity">
              @for (s of seatOptions(); track s) { <option [ngValue]="s">{{ s }} Seater {{ s >= 13 ? 'Tempo Traveller' : 'Car' }}</option> }
            </select>
          </div>
          <div class="fld">
            <label class="lbl" for="ac-fuel">Fuel</label>
            <select id="ac-fuel" class="inp" name="fuel" [(ngModel)]="car.fuelType">
              @for (f of fuels; track f.value) { <option [ngValue]="f.value">{{ f.label }}</option> }
            </select>
          </div>
          <div class="fld">
            <label class="lbl" for="ac-city">Based in (city)</label>
            <input id="ac-city" class="inp" maxlength="100" placeholder="e.g. Bhubaneswar" name="city" [(ngModel)]="car.baseCity" />
          </div>
        </div>
        <label class="row" style="gap:8px;margin-bottom:12px;font-size:13px;cursor:pointer">
          <input type="checkbox" name="ac" [(ngModel)]="car.hasAc" /> Air conditioned
        </label>
        <div class="fld">
          <label class="lbl" for="ac-desc">Description (optional)</label>
          <textarea id="ac-desc" class="inp" rows="3" maxlength="2000" name="desc" [(ngModel)]="car.description"></textarea>
        </div>
      </div>

      @if (error()) { <div class="errorbox">{{ error() }}</div> }
      <div class="row" style="justify-content:flex-end;gap:8px">
        <a class="btn ghost" routerLink="/admin/cars">Cancel</a>
        <button type="submit" class="btn" [disabled]="busy()">
          @if (busy()) { <span class="spin"></span> } @else { Add car and continue }
        </button>
      </div>
      <p class="note" style="text-align:right">Next: add photos and pricing, then publish.</p>
    </form>
  `,
})
export class AdminCarNewComponent implements OnInit {
  private readonly cars = inject(AdminCarService);
  private readonly carService = inject(CarService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly DriverStatus = DriverStatus;
  readonly driverLabels = ApprovalLabels.driver;
  readonly fuels = Object.values(FuelType)
    .filter((v): v is FuelType => typeof v === 'number')
    .map((value) => ({ value, label: FuelTypeLabels[value] }));

  readonly mode = signal<DriverMode>('existing');
  readonly chosen = signal<AdminDriverListItem | null>(null);
  readonly results = signal<AdminDriverListItem[]>([]);
  readonly searching = signal(false);
  /** Seat categories come from the API settings, not from here. */
  readonly seatOptions = signal<number[]>([]);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  search = '';
  newDriver = { name: '', phoneNumber: '', city: '' };
  car = { brand: '', modelName: '', registrationNumber: '', fuelType: FuelType.Diesel, seatCapacity: 7, hasAc: true, description: '', baseCity: '' };

  private readonly search$ = new Subject<string>();

  ngOnInit(): void {
    this.carService.settings().subscribe({
      next: (s) => {
        this.seatOptions.set(s.seatCapacities);
        if (!s.seatCapacities.includes(this.car.seatCapacity)) this.car.seatCapacity = s.seatCapacities[0];
      },
    });

    this.search$
      .pipe(
        debounceTime(250),
        switchMap((term) => {
          this.searching.set(!!term.trim());
          return term.trim() ? this.cars.drivers(AdminCarFilter.All, term, 1, 8) : of(null);
        }),
      )
      .subscribe({
        next: (r) => {
          this.results.set(r?.items ?? []);
          this.searching.set(false);
        },
        error: () => this.searching.set(false),
      });

    // Coming from a driver's page ("Add car"): that driver is pre-selected.
    const driverId = Number(this.route.snapshot.queryParamMap.get('driver'));
    if (driverId) {
      this.cars.driver(driverId).subscribe({
        next: (d) => {
          const p = d.profile;
          this.chosen.set({
            driverId: p.driverId, name: p.name, phoneNumber: p.phoneNumber, city: p.city, profilePhotoUrl: p.profilePhotoUrl,
            status: p.status, statusReason: p.statusReason, awaitingReview: false, submittedForReviewAt: p.submittedForReviewAt,
            approvedAt: p.approvedAt, carCount: d.cars.length, approvedCarCount: 0, createdAt: p.createdAt,
          });
          if (!this.car.baseCity && p.city) this.car.baseCity = p.city;
        },
      });
    }
  }

  onSearch(value: string): void {
    this.search = value;
    this.search$.next(value);
  }

  setNew(): void {
    this.mode.set('new');
    this.chosen.set(null);
    // A typed number or name carries over to the new-driver form.
    const term = this.search.trim();
    if (/^\+?\d[\d\s]*$/.test(term)) this.newDriver.phoneNumber ||= term.replace(/\D/g, '').slice(-10);
    else if (term) this.newDriver.name ||= term;
  }

  save(): void {
    const problem = this.check();
    if (problem) {
      this.error.set(problem);
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const request: SaveCarRequest = {
      brand: this.car.brand.trim(),
      modelName: this.car.modelName.trim(),
      registrationNumber: this.car.registrationNumber.trim(),
      fuelType: this.car.fuelType,
      seatCapacity: this.car.seatCapacity,
      hasAc: this.car.hasAc,
      description: this.car.description.trim() || null,
      baseCity: this.car.baseCity.trim(),
    };

    const driverId$: Observable<number> =
      this.mode() === 'existing'
        ? of(this.chosen()!.driverId)
        : this.cars
            .createDriver({ name: this.newDriver.name.trim(), phoneNumber: this.newDriver.phoneNumber.trim(), city: this.newDriver.city.trim() || null })
            .pipe(
              switchMap((d) => {
                // If the car step fails next, keep the new driver selected instead of creating them twice.
                this.mode.set('existing');
                this.chosen.set({
                  driverId: d.profile.driverId, name: d.profile.name, phoneNumber: d.profile.phoneNumber, city: d.profile.city,
                  profilePhotoUrl: null, status: d.profile.status, statusReason: null, awaitingReview: false, submittedForReviewAt: null,
                  approvedAt: null, carCount: 0, approvedCarCount: 0, createdAt: d.profile.createdAt,
                });
                return of(d.profile.driverId);
              }),
            );

    driverId$.pipe(switchMap((driverId) => this.cars.createCar(driverId, request))).subscribe({
      next: (car) => {
        this.busy.set(false);
        this.toast.success('Car added. Now add photos and pricing, then publish.');
        this.router.navigate(['/admin/cars', car.carId]);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(apiErrorMessage(err, 'Could not add this car.'));
      },
    });
  }

  /** Quick checks before the round trip; the server validates everything again. */
  private check(): string | null {
    if (this.mode() === 'existing') {
      const d = this.chosen();
      if (!d) return 'Choose the car’s driver, or add a new driver.';
      if (d.status === DriverStatus.Suspended) return 'This driver is suspended. Lift the suspension before adding a car.';
    } else {
      if (!this.newDriver.name.trim()) return 'Enter the driver’s full name.';
      if (!/^[6-9]\d{9}$/.test(this.newDriver.phoneNumber.trim())) return 'Enter the driver’s 10-digit WhatsApp number.';
    }
    if (!this.car.brand.trim() || !this.car.modelName.trim()) return 'Enter the car’s brand and model.';
    if (!this.car.registrationNumber.trim()) return 'Enter the registration number.';
    if (!this.car.baseCity.trim()) return 'Enter the city the car is based in.';
    return null;
  }
}
