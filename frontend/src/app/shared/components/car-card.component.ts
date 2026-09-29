import { CommonModule } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CarSearchResult, CarWindow, FuelTypeLabels } from '../../core/models/car.model';
import { ImageUrlPipe } from '../pipes/image-url.pipe';
import { windowToParams } from '../utils/car-format';

/**
 * One car in search results, styled like the trip cards: a clean photo, then colour bands for the
 * name, the four inclusions and the price. The whole card opens the car's details; "Book Now" goes
 * straight to booking. Everything shown comes from the API.
 */
@Component({
  selector: 'app-car-card',
  standalone: true,
  imports: [CommonModule, RouterLink, ImageUrlPipe],
  template: `
    @let c = car();
    <a class="ccard" [attr.data-tone]="tone()" [routerLink]="['/cars', c.carId]" [queryParams]="params()" [attr.aria-label]="c.displayName + ', view details'">
      <div class="cc-shot">
        @if (c.coverPhotoUrl) {
          <img [src]="c.coverPhotoUrl | imageUrl" [alt]="c.displayName" loading="lazy" />
        } @else {
          <span class="cc-noshot" aria-hidden="true">
            <svg width="72" height="72" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 16v-3.2a2 2 0 0 1 .6-1.4L6 9l1.6-2.7A2 2 0 0 1 9.3 5.3h5.4a2 2 0 0 1 1.7 1L18 9l2.4 2.4a2 2 0 0 1 .6 1.4V16a1 1 0 0 1-1 1h-1"></path><path d="M5 17H4a1 1 0 0 1-1-1"></path><path d="M6 9h12"></path><circle cx="7.5" cy="17" r="2"></circle><circle cx="16.5" cy="17" r="2"></circle><path d="M9.5 17h5"></path></svg>
          </span>
        }
      </div>

      <div class="cc-title">
        <h3>{{ c.displayName }}</h3>
        <span class="cc-city">{{ c.baseCity }}</span>
      </div>

      <div class="cc-incl">
        <div class="cc-fi">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"></path><circle cx="9" cy="7" r="4"></circle><path d="M22 21v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8"></path></svg>
          <b>{{ c.seatCapacity }} Seats</b>
        </div>
        <div class="cc-fi">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 22V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v17"></path><path d="M3 22h12M7 8h4"></path><path d="M15 12h2a2 2 0 0 1 2 2v3a1.5 1.5 0 0 0 3 0V9l-3-3"></path></svg>
          <b>{{ fuel() }}</b>
        </div>
        <div class="cc-fi">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 2v20M4.9 4.9l14.2 14.2M2 12h20M4.9 19.1 19.1 4.9"></path></svg>
          <b>{{ c.hasAc ? 'AC' : 'Non-AC' }}</b>
        </div>
        <div class="cc-fi">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"></circle><circle cx="12" cy="12" r="2.5"></circle><path d="M12 14.5V21M9.6 11.3 3.5 9.5M14.4 11.3l6.1-1.8"></path></svg>
          <b>With Driver</b>
        </div>
      </div>

      <div class="cc-price">
        <div class="price"><b>₹{{ c.pricePerKm | number: '1.0-2' }}</b><small>/ km</small></div>
        @if (c.isAvailable) {
          <span class="btn sm" role="button" (click)="book($event)">Book Now</span>
        } @else {
          <span class="btn sm cc-booked" aria-disabled="true" (click)="$event.preventDefault(); $event.stopPropagation()">Booked for this time</span>
        }
      </div>
    </a>
  `,
})
export class CarCardComponent {
  private readonly router = inject(Router);

  readonly car = input.required<CarSearchResult>();
  readonly window = input<CarWindow | null>(null);
  readonly index = input(0);

  readonly fuel = computed(() => FuelTypeLabels[this.car().fuelType]);
  /** Colour family, cycling mint / peach / sky / lavender like the trip cards. */
  readonly tone = computed(() => this.index() % 4);
  readonly params = computed(() => windowToParams(this.window()));

  /** "Book Now" sits inside the card link, so it navigates itself instead of opening the details. */
  book(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.router.navigate(['/cars', this.car().carId, 'book'], { queryParams: this.params() });
  }
}
