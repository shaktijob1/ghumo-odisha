import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { CarPhotoKind, CarPublicDetail, CarWindow, FuelTypeLabels } from '../../core/models/car.model';
import { CarService } from '../../core/services/car.service';
import { CarWindowFormComponent } from '../../shared/components/car-window-form.component';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { apiErrorMessage } from '../../shared/utils/api-error';
import { defaultWindow, durationLabel, timeLabel, windowDateLabel, windowFromParams, windowToParams } from '../../shared/utils/car-format';

/** /cars/:id — photos, specs, pricing (incl. base fare by distance), the driver, and Book Now for the chosen window. */
@Component({
  selector: 'app-car-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, ImageUrlPipe, StatePanelComponent, CarWindowFormComponent],
  template: `
    <div class="container cz-page">
      <a class="cz-back" routerLink="/cars" [queryParams]="params()">← Back to cars</a>

      @switch (state()) {
        @case ('loading') { <app-state-panel kind="loading" message="Loading car…"></app-state-panel> }
        @case ('error') {
          <div class="card pad cz-empty">
            <p>{{ error() }}</p>
            <a class="btn ghost sm" routerLink="/cars" [queryParams]="params()">See other cars</a>
          </div>
        }
        @default {
          @if (car(); as c) {
            @let s = c.summary;
            <div class="cz-detail">
              <div class="cz-main">
                <div class="cz-gallery card">
                  <div class="cz-gallery-main">
                    @if (activePhoto(); as p) {
                      <img [src]="p | imageUrl" [alt]="s.displayName" />
                    } @else {
                      <div class="cz-noimg mut">No photos yet</div>
                    }
                  </div>
                  @if (c.photos.length > 0) {
                    <div class="cz-tabs" role="tablist">
                      <button type="button" role="tab" [class.on]="photoKind() === Exterior" (click)="showKind(Exterior)">Outside ({{ countOf(Exterior) }})</button>
                      <button type="button" role="tab" [class.on]="photoKind() === Interior" (click)="showKind(Interior)">Inside ({{ countOf(Interior) }})</button>
                    </div>
                    <div class="cz-thumbs">
                      @for (p of photosOfKind(); track p.carPhotoId; let i = $index) {
                        <button type="button" [class.on]="photoIndex() === i" (click)="photoIndex.set(i)" [attr.aria-label]="'Photo ' + (i + 1)">
                          <img [src]="p.imageUrl | imageUrl" alt="" loading="lazy" />
                        </button>
                      }
                    </div>
                  }
                </div>

                <div class="card pad">
                  <span class="cz-cat">{{ s.category }}</span>
                  <h1 class="cz-title">{{ s.displayName }}</h1>
                  <div class="cz-chips">
                    <span>{{ fuel() }}</span><span>{{ s.seatCapacity }} Seats</span><span>{{ s.hasAc ? 'AC' : 'Non-AC' }}</span>
                    <span>Driver included</span><span>Pickup: {{ s.baseCity }}</span>
                  </div>
                  @if (c.description) { <p class="cz-desc">{{ c.description }}</p> }
                </div>

                <div class="card pad">
                  <h3 class="cz-h3">Pricing</h3>
                  <div class="kv">
                    <span class="k">Price per km</span><b>₹{{ s.pricePerKm | number: '1.0-2' }}</b>
                    <span class="k">Night halt</span><b>₹{{ s.nightHaltPrice | number: '1.0-0' }} per night the driver stays out</b>
                  </div>
                  <h4 class="cz-h4">Base fare by distance</h4>
                  <table class="cz-table">
                    <tbody>
                      @for (t of tiers(); track $index) {
                        <tr><td>{{ t.label }}</td><td>₹{{ t.fare | number: '1.0-0' }}</td></tr>
                      }
                    </tbody>
                  </table>
                  <p class="note">Total fare = base fare for the distance + km × ₹{{ s.pricePerKm | number: '1.0-2' }} + night halts + tolls/parking if any. You pay a small booking amount now; the final fare uses the actual km on the odometer.</p>
                </div>
              </div>

              <aside class="cz-side">
                <div class="card pad cz-sticky">
                  <h3 class="cz-h3">Your trip</h3>
                  @if (editing()) {
                    <app-car-window-form [initial]="window()" [compact]="true" submitLabel="Check availability" (search)="changeWindow($event.window)"></app-car-window-form>
                  } @else {
                    <div class="cz-when">
                      <div><span class="k">Pickup</span><b>{{ s.baseCity }}</b></div>
                      <div><span class="k">Date</span><b>{{ dateLabel() }}</b></div>
                      <div><span class="k">Time</span><b>{{ timeText() }}</b></div>
                      <div><span class="k">Duration</span><b>{{ hoursText() }}</b></div>
                    </div>
                    <button type="button" class="linkbtn" style="padding:0" (click)="editing.set(true)">Edit</button>
                  }
                  @if (error()) { <p class="cz-ferr">{{ error() }}</p> }
                  <div class="hr"></div>
                  @if (s.isAvailable) {
                    <div class="sumrow"><span>₹{{ s.pricePerKm | number: '1.0-2' }}/km + base fare</span><b>{{ baseFareRange() }}</b></div>
                    <p class="note" style="margin-bottom:10px">You'll see the full fare for your distance on the next step.</p>
                    <span class="badge ok">Available</span>
                    <a class="btn block" style="margin-top:12px" [routerLink]="['/cars', s.carId, 'book']" [queryParams]="params()">Book Now</a>
                  } @else {
                    <span class="badge bad">Booked for this time</span>
                    <p class="note" style="margin-top:8px">Pick another time, or see other cars.</p>
                  }
                </div>

                <div class="card pad cz-driver">
                  @if (c.driver.profilePhotoUrl) {
                    <img [src]="c.driver.profilePhotoUrl | imageUrl" alt="" />
                  }
                  <div>
                    <span class="k">Your driver</span>
                    <b>{{ c.driver.firstName }}</b>
                    @if (c.driver.experienceYears) { <span class="mut">{{ c.driver.experienceYears }} years driving</span> }
                    <span class="note">Verified by Ghumo Odisha. Contact details are shared once you book.</span>
                  </div>
                </div>
              </aside>
            </div>
          }
        }
      }
    </div>
  `,
})
export class CarDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly carService = inject(CarService);

  readonly Exterior = CarPhotoKind.Exterior;
  readonly Interior = CarPhotoKind.Interior;

  private readonly carId = toSignal(this.route.paramMap.pipe(map((p) => Number(p.get('id')))), { requireSync: true });
  readonly window = toSignal(this.route.queryParamMap.pipe(map((p) => windowFromParams(p) ?? defaultWindow())), { requireSync: true });
  readonly params = computed(() => windowToParams(this.window()));

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly error = signal('');
  readonly car = signal<CarPublicDetail | null>(null);
  readonly editing = signal(false);
  readonly photoKind = signal<CarPhotoKind>(CarPhotoKind.Exterior);
  readonly photoIndex = signal(0);

  readonly fuel = computed(() => (this.car() ? FuelTypeLabels[this.car()!.summary.fuelType] : ''));
  readonly photosOfKind = computed(() => (this.car()?.photos ?? []).filter((p) => p.kind === this.photoKind()));
  readonly activePhoto = computed(() => this.photosOfKind()[this.photoIndex()]?.imageUrl ?? this.car()?.photos[0]?.imageUrl ?? null);
  readonly dateLabel = computed(() => windowDateLabel(this.window().date));
  readonly timeText = computed(() => timeLabel(this.window().time));
  readonly hoursText = computed(() => durationLabel(this.window().hours));

  readonly baseFareRange = computed(() => {
    const s = this.car()?.summary;
    if (!s) return '';
    const fmt = (n: number) => `₹${n.toLocaleString('en-IN')}`;
    return s.baseFareFrom === s.baseFareTo ? fmt(s.baseFareFrom) : `${fmt(s.baseFareFrom)} – ${fmt(s.baseFareTo)}`;
  });

  /** "Up to 50 km", "51 – 100 km", "Above 200 km". */
  readonly tiers = computed(() => {
    const list = this.car()?.baseFareTiers ?? [];
    let previous = 0;
    return list.map((t) => {
      const label = t.upToKm === null ? (previous === 0 ? 'Any distance' : `Above ${previous} km`) : previous === 0 ? `Up to ${t.upToKm} km` : `${previous + 1} – ${t.upToKm} km`;
      if (t.upToKm !== null) previous = t.upToKm;
      return { label, fare: t.baseFare };
    });
  });

  constructor() {
    effect(() => this.load(this.carId(), this.window()));
  }

  countOf(kind: CarPhotoKind): number {
    return (this.car()?.photos ?? []).filter((p) => p.kind === kind).length;
  }

  showKind(kind: CarPhotoKind): void {
    this.photoKind.set(kind);
    this.photoIndex.set(0);
  }

  changeWindow(window: CarWindow): void {
    this.editing.set(false);
    this.router.navigate([], { relativeTo: this.route, queryParams: windowToParams(window), replaceUrl: true });
  }

  private load(id: number, window: CarWindow): void {
    // Keep the page on screen while re-checking availability for a new time.
    if (!this.car()) this.state.set('loading');
    this.carService.getCar(id, window).subscribe({
      next: (c) => {
        this.car.set(c);
        this.error.set('');
        this.state.set('ready');
      },
      error: (e: unknown) => {
        this.error.set(apiErrorMessage(e, 'Could not load this car.'));
        if (!this.car()) this.state.set('error');
      },
    });
  }
}
