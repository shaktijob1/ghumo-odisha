import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CarWindow } from '../../core/models/car.model';
import { CarService } from '../../core/services/car.service';
import { DatePickerComponent } from './date-picker.component';
import { DURATION_PRESETS, PICKUP_TIMES, istDateValue } from '../utils/car-format';

/**
 * Pickup location (optional), date, time and duration (12 h / 24 h / days / custom). Used by the
 * Cars search page and — without the location — to edit the window on the booking page.
 */
@Component({
  selector: 'app-car-window-form',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePickerComponent],
  template: `
    <form class="cz-wform" [class.compact]="compact()" (ngSubmit)="submit()">
      @if (locations(); as locs) {
        <div class="fld">
          <label class="lbl" for="cz-city">Pickup location</label>
          <select id="cz-city" class="inp" name="city" [ngModel]="city()" (ngModelChange)="city.set($event)">
            <option value="">Any location</option>
            @for (l of locs; track l) {
              <option [value]="l">{{ l }}</option>
            }
          </select>
        </div>
      }
      <div class="fld">
        <span class="lbl">Pickup date</span>
        <app-date-picker [value]="date()" (valueChange)="date.set($event)" [min]="today" placeholder="Select date"></app-date-picker>
      </div>
      <div class="fld">
        <label class="lbl" for="cz-time">Pickup time</label>
        <select id="cz-time" class="inp" name="time" [ngModel]="time()" (ngModelChange)="time.set($event)">
          @for (t of times; track t.value) {
            <option [value]="t.value">{{ t.label }}</option>
          }
        </select>
      </div>
      <div class="fld">
        <label class="lbl" for="cz-dur">Duration</label>
        <select id="cz-dur" class="inp" name="duration" [ngModel]="durationChoice()" (ngModelChange)="durationChoice.set($event)">
          @for (d of presets; track d.hours) {
            <option [ngValue]="d.hours">{{ d.label }}</option>
          }
          <option [ngValue]="0">Custom duration</option>
        </select>
      </div>
      @if (durationChoice() === 0) {
        <div class="fld cz-custom">
          <label class="lbl" for="cz-custom">Custom duration</label>
          <div class="row">
            <input id="cz-custom" class="inp" type="number" name="customValue" min="1" inputmode="numeric"
              [ngModel]="customValue()" (ngModelChange)="customValue.set(+$event)" />
            <select class="inp" name="customUnit" aria-label="Unit" [ngModel]="customUnit()" (ngModelChange)="customUnit.set($event)">
              <option value="hours">Hours</option>
              <option value="days">Days</option>
            </select>
          </div>
        </div>
      }
      <div class="cz-wform-go">
        <button type="submit" class="btn" [disabled]="!!error()">{{ submitLabel() }}</button>
      </div>
      @if (error(); as e) {
        <p class="cz-ferr">{{ e }}</p>
      }
    </form>
  `,
})
export class CarWindowFormComponent implements OnInit {
  private readonly carService = inject(CarService);

  readonly initial = input<CarWindow | null>(null);
  readonly initialCity = input<string | null>(null);
  /** Pass the list to show the pickup-location field; null hides it. */
  readonly locations = input<string[] | null>(null);
  readonly submitLabel = input('Search Cars');
  readonly compact = input(false);

  readonly search = output<{ window: CarWindow; city: string | null }>();

  readonly today = istDateValue(0);
  readonly times = PICKUP_TIMES;
  readonly presets = DURATION_PRESETS;

  readonly city = signal('');
  readonly date = signal(istDateValue(1));
  readonly time = signal('10:00');
  /** Preset hours, or 0 for custom. */
  readonly durationChoice = signal(12);
  readonly customValue = signal(5);
  readonly customUnit = signal<'hours' | 'days'>('days');

  private readonly limits = signal({ min: 4, max: 720 });

  readonly hours = computed(() => {
    if (this.durationChoice() !== 0) return this.durationChoice();
    const v = Math.floor(this.customValue() || 0);
    return this.customUnit() === 'days' ? v * 24 : v;
  });

  readonly error = computed(() => {
    if (!this.date()) return 'Choose a pickup date.';
    const { min, max } = this.limits();
    const h = this.hours();
    if (!h || h < min || h > max) return `Duration must be between ${min} hours and ${max / 24} days.`;
    return null;
  });

  constructor() {
    // Re-apply when the parent passes a different starting window (e.g. after navigation).
    effect(() => {
      const w = this.initial();
      if (!w) return;
      this.date.set(w.date);
      this.time.set(w.time);
      if (this.presets.some((p) => p.hours === w.hours)) {
        this.durationChoice.set(w.hours);
      } else {
        this.durationChoice.set(0);
        const days = w.hours % 24 === 0;
        this.customUnit.set(days ? 'days' : 'hours');
        this.customValue.set(days ? w.hours / 24 : w.hours);
      }
    });
    effect(() => this.city.set(this.initialCity() ?? ''));
  }

  ngOnInit(): void {
    this.carService.settings().subscribe({
      next: (s) => this.limits.set({ min: s.minDurationHours, max: s.maxDurationHours }),
      error: () => undefined,
    });
  }

  submit(): void {
    if (this.error()) return;
    this.search.emit({ window: { date: this.date(), time: this.time(), hours: this.hours() }, city: this.city() || null });
  }
}
