import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { CarSearchResult, CarWindow } from '../../core/models/car.model';
import { CarService } from '../../core/services/car.service';
import { CarCardComponent } from './car-card.component';
import { StatePanelComponent } from './state-panel.component';
import { apiErrorMessage } from '../utils/api-error';
import { durationLabel, timeLabel, windowDateLabel } from '../utils/car-format';

/**
 * "Available Cars" for a search: seat filters (from the API's seat categories), and car cards with
 * loading / empty / error states. Only approved cars ever come back from the API.
 */
@Component({
  selector: 'app-car-results',
  standalone: true,
  imports: [CommonModule, CarCardComponent, StatePanelComponent],
  template: `
    <section id="car-results" class="sect tight">
      <div class="container">
        <div class="crpanel">
          <div class="crhead">
            <div class="crhead-text">
              <h2><span class="up-g">Available </span><span class="up-o">Cars</span></h2>
              <p class="sub">{{ summary() }}</p>
            </div>
            @if (state() === 'ready') {
              <span class="sechead-meta crcount sec-count"><b>{{ visible().length }}</b> {{ visible().length === 1 ? 'car' : 'cars' }}</span>
            }
          </div>

          @if (seatOptions().length > 1) {
            <div class="locfilters crfilters" role="group" aria-label="Filter by seats">
              <button type="button" class="locbtn" [class.on]="seatFilter() === null" [attr.aria-pressed]="seatFilter() === null" (click)="seatFilter.set(null)">All</button>
              @for (s of seatOptions(); track s) {
                <button type="button" class="locbtn" [class.on]="seatFilter() === s" [attr.aria-pressed]="seatFilter() === s" (click)="seatFilter.set(s)">{{ s }} Seater</button>
              }
            </div>
          }

          @switch (state()) {
            @case ('loading') {
              <app-state-panel kind="loading" message="Finding cars…"></app-state-panel>
            }
            @case ('error') {
              <div class="state-panel">
                <p class="mut">{{ errorMessage() }}</p>
                <button type="button" class="btn ghost sm" style="margin-top:12px" (click)="load()">Try again</button>
              </div>
            }
            @default {
              @if (visible().length === 0) {
                <app-state-panel kind="empty"
                  [message]="cars().length === 0 ? 'No cars at this pickup location yet. Try another location, or message us on WhatsApp.' : 'No cars with this many seats. Try another option.'"></app-state-panel>
              } @else {
                <div class="crgrid">
                  @for (c of visible(); track c.carId; let i = $index) {
                    <app-car-card [car]="c" [window]="window()" [index]="i"></app-car-card>
                  }
                </div>
                <p class="note crnote">Every car comes with its driver. Fares are estimates — the final fare uses the actual km driven.</p>
              }
            }
          }
        </div>
      </div>
    </section>
  `,
})
export class CarResultsComponent {
  private readonly carService = inject(CarService);

  readonly city = input<string | null>(null);
  readonly window = input<CarWindow | null>(null);

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly errorMessage = signal('Could not load cars. Please check your connection.');
  readonly cars = signal<CarSearchResult[]>([]);
  readonly seatFilter = signal<number | null>(null);

  /** Only seat categories that actually have cars in these results. */
  readonly seatOptions = computed(() => [...new Set(this.cars().map((c) => c.seatCapacity))].sort((a, b) => a - b));
  readonly visible = computed(() => {
    const s = this.seatFilter();
    return s === null ? this.cars() : this.cars().filter((c) => c.seatCapacity === s);
  });

  readonly summary = computed(() => {
    const w = this.window();
    return [
      this.city() ? `Pickup from ${this.city()}` : 'All pickup locations',
      w ? `${windowDateLabel(w.date)}, ${timeLabel(w.time)}` : null,
      w ? durationLabel(w.hours) : null,
    ]
      .filter(Boolean)
      .join(' · ');
  });

  constructor() {
    effect(() => {
      this.city();
      this.window();
      this.load();
    });
  }

  load(): void {
    this.state.set('loading');
    this.seatFilter.set(null);
    this.carService.search({ city: this.city(), window: this.window() }).subscribe({
      next: (r) => {
        this.cars.set(r.cars);
        this.state.set('ready');
      },
      error: (e: unknown) => {
        this.errorMessage.set(apiErrorMessage(e, 'Could not load cars. Please try again.'));
        this.state.set('error');
      },
    });
  }
}

