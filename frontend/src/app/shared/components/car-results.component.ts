import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { CarWithFare } from '../../core/models/car.model';
import { CarService } from '../../core/services/car.service';
import { CarCardComponent } from './car-card.component';
import { StatePanelComponent } from './state-panel.component';
import { apiErrorMessage } from '../utils/api-error';
import { VehicleSearch } from '../utils/vehicle-search';

/**
 * "Available Vehicles" for a search: each vehicle with the fare the server worked out for this exact
 * pickup, drop and time, seat filters, and loading / empty / error / outside-area states.
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
              <h2><span class="up-g">Available </span><span class="up-o">Vehicles</span></h2>
            </div>
            @if (state() === 'ready') {
              <span class="sechead-meta crcount sec-count"><b>{{ visible().length }}</b> {{ visible().length === 1 ? 'vehicle' : 'vehicles' }}</span>
            }
          </div>

          @if (state() === 'ready' && seatOptions().length > 1) {
            <div class="locfilters crfilters" role="group" aria-label="Filter by seats">
              <button type="button" class="locbtn" [class.on]="seatFilter() === null" [attr.aria-pressed]="seatFilter() === null" (click)="seatFilter.set(null)">All</button>
              @for (s of seatOptions(); track s) {
                <button type="button" class="locbtn" [class.on]="seatFilter() === s" [attr.aria-pressed]="seatFilter() === s" (click)="seatFilter.set(s)">{{ s }} Seater</button>
              }
            </div>
          }

          @switch (state()) {
            @case ('loading') {
              <app-state-panel kind="loading" message="Working out fares…"></app-state-panel>
            }
            @case ('error') {
              <div class="state-panel">
                <p class="mut">{{ errorMessage() }}</p>
                <button type="button" class="btn ghost sm" style="margin-top:12px" (click)="load()">Try again</button>
              </div>
            }
            @case ('outside') {
              <app-state-panel kind="empty" [message]="errorMessage() + ' Please choose a pickup inside our area.'"></app-state-panel>
            }
            @default {
              @if (visible().length === 0) {
                <app-state-panel kind="empty"
                  [message]="items().length === 0 ? 'No vehicles for this time yet. Try another time, or message us on WhatsApp.' : 'No vehicles with this many seats. Try another option.'"></app-state-panel>
              } @else {
                <div class="crgrid">
                  @for (i of visible(); track i.car.carId) {
                    <app-car-card [item]="i" [search]="search()"></app-car-card>
                  }
                </div>
                <p class="note crnote">Every vehicle comes with its driver. Fares are estimates from Google Maps road distances — the final fare uses the actual km driven.</p>
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

  readonly search = input.required<VehicleSearch>();

  readonly state = signal<'loading' | 'ready' | 'error' | 'outside'>('loading');
  readonly errorMessage = signal('Could not load vehicles. Please check your connection.');
  readonly items = signal<CarWithFare[]>([]);
  readonly seatFilter = signal<number | null>(null);

  /** Only seat categories that actually have vehicles in these results. */
  readonly seatOptions = computed(() => [...new Set(this.items().map((i) => i.car.seatCapacity))].sort((a, b) => a - b));
  readonly visible = computed(() => {
    const s = this.seatFilter();
    return s === null ? this.items() : this.items().filter((i) => i.car.seatCapacity === s);
  });

  private sub?: Subscription;

  constructor() {
    effect(() => {
      this.search();
      this.load();
    });
  }

  load(): void {
    this.sub?.unsubscribe();
    this.state.set('loading');
    this.seatFilter.set(null);
    this.sub = this.carService.fares(this.search()).subscribe({
      next: (r) => {
        this.items.set(r.cars);
        if (!r.isServiceable) {
          this.errorMessage.set(r.message ?? 'Vehicles don’t pick up from there yet.');
          this.state.set('outside');
          return;
        }
        this.state.set('ready');
      },
      error: (e: unknown) => {
        this.errorMessage.set(apiErrorMessage(e, 'Could not load vehicles. Please try again.'));
        this.state.set('error');
      },
    });
  }
}
