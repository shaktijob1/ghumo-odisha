import { Component, ElementRef, HostListener, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { TripPlace } from '../../core/models/location.model';
import { CarService } from '../../core/services/car.service';
import { PickedPlace } from '../../core/services/google-maps.service';
import { DURATION_PRESETS, PICKUP_TIMES, durationLabel, istDateValue, timeLabel } from '../utils/car-format';
import { VehicleSearch } from '../utils/vehicle-search';
import { DatePickerComponent } from './date-picker.component';
import { MapPickDialogComponent } from './map-pick-dialog.component';
import { PlaceSearchComponent } from './place-search.component';

type Dropdown = 'time' | 'duration';
type MapTarget = 'pickup' | 'drop';

/**
 * The Vehicles search, in the home search card's style (same fields as the Trips tab):
 * Date · Pickup location · Drop location · Pickup time · Duration · Search.
 * The pickup is checked against the service areas as soon as it's chosen; Search stays blocked outside them.
 */
@Component({
  selector: 'app-vehicle-search-form',
  standalone: true,
  imports: [FormsModule, DatePickerComponent, PlaceSearchComponent, MapPickDialogComponent],
  template: `
    <div class="vs-trip" role="radiogroup" aria-label="Trip type">
      <button type="button" role="radio" [class.on]="!roundTrip()" [attr.aria-checked]="!roundTrip()" (click)="roundTrip.set(false)">
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 12h14M13 6l6 6-6 6"></path></svg>
        One way
      </button>
      <button type="button" role="radio" [class.on]="roundTrip()" [attr.aria-checked]="roundTrip()" (click)="roundTrip.set(true)">
        <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M4 9h14l-4-4M20 15H6l4 4"></path></svg>
        Round trip
      </button>
    </div>
    <span class="vs-trip-note">{{ roundTrip() ? 'Back to your pickup at the end.' : 'Drop at where you’re going.' }}</span>
    <div class="vs">
      <!-- 1. Date -->
      <div class="hx-field vs-date">
        <span class="hx-lbl">
          <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="3" y="4" width="18" height="18" rx="2"></rect><path d="M16 2v4M8 2v4M3 10h18"></path></svg>
          Date
        </span>
        <div class="hx-datebox">
          <app-date-picker [value]="date()" (valueChange)="date.set($event)" placeholder="Choose date" [min]="today"></app-date-picker>
          <svg class="hx-chev" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"></path></svg>
        </div>
      </div>

      <!-- 2. Pickup -->
      <div class="hx-field vs-pickup">
        <label class="hx-lbl" for="vs-pickup">
          <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 21s-6.5-5.9-6.5-11A6.5 6.5 0 0 1 18.5 10c0 5.1-6.5 11-6.5 11z"></path><circle cx="12" cy="10" r="2.3"></circle></svg>
          Pickup Location
        </label>
        <app-place-search variant="hero" inputId="vs-pickup" placeholder="Area, hotel or landmark" [allowCurrentLocation]="true" [allowMap]="true"
          [value]="pickup()?.label ?? null" (picked)="setPickup($event)" (cleared)="setPickup(null)" (mapRequested)="mapFor.set('pickup')"></app-place-search>
        @switch (pickupStatus()) {
          @case ('checking') { <span class="vs-note">Checking this area…</span> }
          @case ('ok') { <span class="vs-note ok">✓ Vehicles pick up here</span> }
          @case ('no') { <span class="vs-note no">{{ pickupMessage() }} <button type="button" (click)="mapFor.set('pickup')">See areas on map</button></span> }
        }
      </div>

      <!-- 3. Drop -->
      <div class="hx-field vs-drop">
        <label class="hx-lbl" for="vs-drop">
          <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M4 22V4a1 1 0 0 1 1-1h10l-2 4 2 4H5"></path></svg>
          Where To
        </label>
        <app-place-search variant="hero" inputId="vs-drop" placeholder="Where are you going?" [allowCurrentLocation]="true" [allowMap]="true"
          [value]="drop()?.label ?? null" (picked)="drop.set($event)" (cleared)="drop.set(null)" (mapRequested)="mapFor.set('drop')"></app-place-search>
      </div>

      <!-- 4. Pickup time -->
      <div class="hx-field hx-dd vs-time" [class.open]="open() === 'time'">
        <label class="hx-lbl" for="vs-time">
          <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"></circle><path d="M12 7v5l3 2"></path></svg>
          Pickup Time
        </label>
        <button type="button" id="vs-time" class="hx-box" aria-haspopup="listbox" [attr.aria-expanded]="open() === 'time'" (click)="toggle('time')">
          <span class="hx-val">{{ timeText() }}</span>
          <svg class="hx-chev" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"></path></svg>
        </button>
        @if (open() === 'time') {
          <div class="hs-pop hs-places hx-timelist" role="listbox" aria-label="Pickup time">
            @for (t of times; track t.value) {
              <button type="button" role="option" [class.on]="time() === t.value" [attr.aria-selected]="time() === t.value" (click)="time.set(t.value); open.set(null)">{{ t.label }}</button>
            }
          </div>
        }
      </div>

      <!-- 5. Duration -->
      <div class="hx-field hx-dd vs-dur" [class.open]="open() === 'duration'">
        <label class="hx-lbl" for="vs-dur">
          <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 22h14M5 2h14M17 22v-4.2a2 2 0 0 0-.6-1.4L12 12l-4.4 4.4a2 2 0 0 0-.6 1.4V22M7 2v4.2a2 2 0 0 0 .6 1.4L12 12l4.4-4.4a2 2 0 0 0 .6-1.4V2"></path></svg>
          Duration
        </label>
        <button type="button" id="vs-dur" class="hx-box" aria-haspopup="listbox" [attr.aria-expanded]="open() === 'duration'" (click)="toggle('duration')">
          <span class="hx-val">{{ hoursText() }}</span>
          <svg class="hx-chev" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"></path></svg>
        </button>
        @if (open() === 'duration') {
          <div class="hs-pop hs-places" role="listbox" aria-label="Duration">
            @for (d of presets; track d.hours) {
              <button type="button" role="option" [class.on]="!custom() && hours() === d.hours" [attr.aria-selected]="!custom() && hours() === d.hours" (click)="pickHours(d.hours)">{{ d.label }}</button>
            }
            <button type="button" role="option" [class.on]="custom()" [attr.aria-selected]="custom()" (click)="custom.set(true)">Custom duration…</button>
            @if (custom()) {
              <div class="hx-custom">
                <input class="inp" type="number" min="1" inputmode="numeric" aria-label="How long" [ngModel]="customValue()" (ngModelChange)="customValue.set(+$event)" />
                <select class="inp" aria-label="Unit" [ngModel]="customUnit()" (ngModelChange)="customUnit.set($event)">
                  <option value="hours">Hours</option>
                  <option value="days">Days</option>
                </select>
                <button type="button" class="btn sm" (click)="applyCustom()">Done</button>
              </div>
            }
          </div>
        }
      </div>

      <!-- 6. Search -->
      <button type="button" class="hx-go vs-go" (click)="submit()">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.3" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="11" cy="11" r="7"></circle><path d="m21 21-4.3-4.3"></path></svg>
        Search Vehicles
      </button>
    </div>
    @if (formError()) { <p class="vs-err" role="alert">{{ formError() }}</p> }

    @if (mapFor(); as target) {
      <app-map-pick-dialog
        [title]="target === 'pickup' ? 'Choose pickup on map' : 'Choose where to on map'"
        [initial]="target === 'pickup' ? pickup() : drop()"
        [showZones]="target === 'pickup'"
        (picked)="fromMap(target, $event)"
        (closed)="mapFor.set(null)"
      ></app-map-pick-dialog>
    }
  `,
  styles: `
    :host { display: block; flex: 1 1 auto; min-width: 0; }
    .vs {
      display: grid; gap: 18px 20px; align-items: start;
      grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr) minmax(0, 1.4fr);
      grid-template-areas: 'date pickup drop' 'time dur go';
    }
    .vs-date { grid-area: date; } .vs-pickup { grid-area: pickup; } .vs-drop { grid-area: drop; }
    .vs-time { grid-area: time; } .vs-dur { grid-area: dur; }
    .vs-go { grid-area: go; align-self: end; min-width: 0; width: 100%; height: 54px; padding: 0 14px; gap: 10px; font-size: 17px; white-space: nowrap; }
    .vs-note { display: block; margin-top: 6px; font-size: 12px; color: var(--muted); }
    .vs-note.ok { color: var(--ok); font-weight: 600; }
    .vs-note.no { color: var(--danger); font-weight: 500; }
    .vs-note button { border: none; background: none; padding: 0; font: 600 12px var(--font-body); color: var(--accent); text-decoration: underline; cursor: pointer; }
    .vs-err { margin: 14px 0 0; color: var(--danger); font-size: 13px; font-weight: 500; }
    .vs-trip { display: inline-flex; gap: 4px; margin-bottom: 18px; padding: 4px; border: 1px solid var(--line); border-radius: 999px; background: var(--canvas); }
    .vs-trip button { display: inline-flex; align-items: center; gap: 7px; height: 38px; padding: 0 18px; border: none; border-radius: 999px; background: none; font: 600 14px var(--font-body); color: var(--muted); cursor: pointer; transition: background-color .15s ease, color .15s ease; }
    .vs-trip button.on { background: var(--accent); color: var(--accent-ink); box-shadow: 0 6px 14px rgba(15,111,92,.22); }
    .vs-trip button:not(.on):hover { color: var(--ink); }
    .vs-trip-note { display: inline-block; margin-left: 12px; font-size: 12px; color: var(--muted); }
    @media (max-width: 1100px) {
      .vs { grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); grid-template-areas: 'date time' 'pickup pickup' 'drop drop' 'dur go'; }
    }
    @media (max-width: 640px) {
      .vs { grid-template-columns: minmax(0, 1fr); grid-template-areas: 'date' 'pickup' 'drop' 'time' 'dur' 'go'; gap: 16px; }
      .vs-go { height: 56px; }
      .vs-trip { display: flex; margin-bottom: 8px; }
      .vs-trip button { flex: 1; justify-content: center; }
      .vs-trip-note { display: block; margin: 0 0 14px 4px; }
    }
  `,
})
export class VehicleSearchFormComponent implements OnInit {
  private readonly cars = inject(CarService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly initial = input<VehicleSearch | null>(null);
  readonly search = output<VehicleSearch>();

  readonly today = istDateValue(0);
  readonly times = PICKUP_TIMES;
  readonly presets = DURATION_PRESETS;

  /** Tomorrow by default: pickups need a couple of hours' notice. */
  readonly date = signal(istDateValue(1));
  readonly time = signal('10:00');
  readonly hours = signal(12);
  readonly custom = signal(false);
  readonly customValue = signal(2);
  readonly customUnit = signal<'hours' | 'days'>('days');
  readonly pickup = signal<TripPlace | null>(null);
  /** "Where to" (the API calls it the drop). */
  readonly drop = signal<TripPlace | null>(null);
  readonly roundTrip = signal(false);

  readonly pickupStatus = signal<'none' | 'checking' | 'ok' | 'no'>('none');
  readonly pickupMessage = signal<string | null>(null);
  readonly open = signal<Dropdown | null>(null);
  readonly mapFor = signal<MapTarget | null>(null);
  readonly formError = signal<string | null>(null);

  readonly timeText = computed(() => timeLabel(this.time()));
  readonly hoursText = computed(() => durationLabel(this.hours()));

  private checkSub?: Subscription;

  ngOnInit(): void {
    const s = this.initial();
    if (!s) return;
    this.date.set(s.window.date);
    this.time.set(s.window.time);
    this.hours.set(s.window.hours);
    this.custom.set(!this.presets.some((p) => p.hours === s.window.hours));
    this.drop.set(s.drop);
    this.roundTrip.set(s.roundTrip);
    this.setPickup(s.pickup);
  }

  toggle(key: Dropdown): void {
    this.open.update((o) => (o === key ? null : key));
    if (this.open() === key) setTimeout(() => this.revealDropdown());
  }

  /**
   * An opened list can land below the screen's edge (on phones it opens in the page flow, under a tall
   * form). Centre the list on the chosen option, then scroll so the field and its list are on screen.
   */
  private revealDropdown(): void {
    const field = this.host.nativeElement.querySelector<HTMLElement>('.hx-dd.open');
    const list = field?.querySelector<HTMLElement>('.hs-pop');
    if (!field || !list) return;
    const chosen = list.querySelector<HTMLElement>('button.on');
    if (chosen) {
      const offset = chosen.getBoundingClientRect().top - list.getBoundingClientRect().top + list.scrollTop;
      list.scrollTop = Math.max(0, offset - (list.clientHeight - chosen.offsetHeight) / 2);
    }

    const smooth = !window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const top = field.getBoundingClientRect().top;
    const bottom = list.getBoundingClientRect().bottom;
    if (bottom > window.innerHeight - 12 || top < 0) {
      // Field label at the top of the screen, with a little air — the list fits under it.
      window.scrollTo({ top: Math.max(0, window.scrollY + top - 12), behavior: smooth ? 'smooth' : 'auto' });
    }
  }

  pickHours(h: number): void {
    this.custom.set(false);
    this.hours.set(h);
    this.open.set(null);
  }

  applyCustom(): void {
    const v = Math.floor(this.customValue() || 0);
    if (v < 1) return;
    this.hours.set(this.customUnit() === 'days' ? v * 24 : v);
    this.open.set(null);
  }

  setPickup(place: TripPlace | null): void {
    this.pickup.set(place);
    this.formError.set(null);
    this.checkSub?.unsubscribe();
    this.pickupMessage.set(null);
    if (!place) {
      this.pickupStatus.set('none');
      return;
    }
    this.pickupStatus.set('checking');
    this.checkSub = this.cars.checkPickup(place).subscribe({
      next: (r) => {
        this.pickupStatus.set(r.isServiceable ? 'ok' : 'no');
        this.pickupMessage.set(r.message);
      },
      // The fare search checks the area again, so a failed early check stays quiet.
      error: () => this.pickupStatus.set('none'),
    });
  }

  fromMap(target: MapTarget, place: PickedPlace): void {
    this.mapFor.set(null);
    if (target === 'pickup') this.setPickup(place);
    else this.drop.set(place);
  }

  submit(): void {
    const pickup = this.pickup();
    const drop = this.drop();
    const error = !pickup
      ? 'Choose your pickup location.'
      : this.pickupStatus() === 'no'
        ? (this.pickupMessage() ?? 'Vehicles don’t pick up from there yet.')
        : !drop
          ? 'Choose where you’re going.'
          : null;
    this.formError.set(error);
    if (error) return;
    this.open.set(null);
    this.search.emit({
      window: { date: this.date(), time: this.time(), hours: this.hours() },
      pickup: pickup!,
      drop: drop!,
      roundTrip: this.roundTrip(),
    });
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(e: MouseEvent): void {
    if (!(e.target as Element | null)?.closest('app-vehicle-search-form .hx-dd')) this.open.set(null);
  }
}
