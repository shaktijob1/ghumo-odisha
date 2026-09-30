import { CommonModule } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CarWithFare, FuelTypeLabels } from '../../core/models/car.model';
import { ImageUrlPipe } from '../pipes/image-url.pipe';
import { VehicleSearch, searchToParams } from '../utils/vehicle-search';

/**
 * A vehicle in the search results, built like the trip card (same classes): photo with the name on it,
 * seats badge and the "Book for ₹99" star, the key facts, then this search's calculated fare + Book Now.
 * The fare comes from the server for exactly this pickup, drop and time.
 */
@Component({
  selector: 'app-car-card',
  standalone: true,
  imports: [CommonModule, RouterLink, ImageUrlPipe],
  template: `
    @let c = item().car;
    @let f = item().fare;
    <a class="tcard vcard" [class.vcard-off]="!bookable()" [routerLink]="['/cars', c.carId, 'book']" [queryParams]="params()"
       [attr.aria-label]="'Book ' + c.displayName">
      <div class="shot">
        @if (c.coverPhotoUrl) {
          <img [src]="c.coverPhotoUrl | imageUrl" [alt]="c.displayName" loading="lazy" />
        } @else {
          <div class="noshot">
            <svg width="44" height="44" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 17H3.5a1 1 0 0 1-1-1v-3.2a2 2 0 0 1 .6-1.4L5 9.5l1.7-3.4A2 2 0 0 1 8.5 5h7a2 2 0 0 1 1.8 1.1L19 9.5l1.9 1.9a2 2 0 0 1 .6 1.4V16a1 1 0 0 1-1 1H19"></path><circle cx="7.5" cy="17" r="2"></circle><circle cx="16.5" cy="17" r="2"></circle><path d="M9.5 17h5"></path></svg>
          </div>
        }
        <span class="durbadge">{{ c.seatCapacity }} Seats</span>
        @if (f && bookable()) {
          <div class="pricestar">
            <span class="pricestar-top">Book for</span>
            <span class="pricestar-amt">₹{{ f.bookingAmount | number: '1.0-0' }}</span>
          </div>
        }
        <div class="heroinfo">
          <h3>{{ c.displayName }}</h3>
          <div class="herometa">
            <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 21s-6.5-5.9-6.5-11A6.5 6.5 0 0 1 18.5 10c0 5.1-6.5 11-6.5 11z"></path><circle cx="12" cy="10" r="2.3"></circle></svg>
            {{ c.baseCity }} · {{ c.category }}
          </div>
        </div>
      </div>

      <div class="body">
        <div class="featrow">
          <div class="fi">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M4 19h16M4 19l4-14h8l4 14"></path><path d="M12 7v2M12 12v2"></path></svg>
            <div class="fi-label"><b>{{ f ? (f.estimatedKm | number) + ' km' : '—' }}</b><span class="fi-sub">{{ f?.roundTrip ? 'round trip' : 'one way' }}</span></div>
          </div>
          <div class="fi">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 22V5a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v17"></path><path d="M3 22h12M7 8h4"></path><path d="M15 12h2a2 2 0 0 1 2 2v3a1.5 1.5 0 0 0 3 0V9l-3-3"></path></svg>
            <div class="fi-label"><b>{{ fuel() }}</b><span class="fi-sub">fuel</span></div>
          </div>
          <div class="fi">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 2v20M4.9 6l14.2 12M4.9 18 19.1 6"></path></svg>
            <div class="fi-label"><b>{{ c.hasAc ? 'AC' : 'Non-AC' }}</b><span class="fi-sub">cabin</span></div>
          </div>
          <div class="fi">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"></circle><circle cx="12" cy="12" r="2.5"></circle><path d="M12 14.5V21M9.6 11.3 3.5 9.5M14.4 11.3l6.1-1.8"></path></svg>
            <div class="fi-label"><b>Driver</b><span class="fi-sub">included</span></div>
          </div>
        </div>

        <div class="pricerow">
          @if (f && f.estimatedKm > 0) {
            <div class="price"><b>₹{{ f.estimatedTotal | number: '1.0-0' }}</b><small>est. total · ₹{{ c.pricePerKm | number: '1.0-2' }}/km</small></div>
          } @else {
            <div class="price"><b>₹{{ c.pricePerKm | number: '1.0-2' }}</b><small>/ km</small></div>
          }
          @if (bookable()) {
            <span class="btn sm">Book Now</span>
          } @else {
            <span class="btn sm vcard-na">{{ !c.isAvailable ? 'Booked' : 'Unavailable' }}</span>
          }
        </div>
        @if (!bookable()) { <p class="vcard-why">{{ reason() }}</p> }
      </div>
    </a>
  `,
})
export class CarCardComponent {
  readonly item = input.required<CarWithFare>();
  readonly search = input.required<VehicleSearch>();

  readonly fuel = computed(() => FuelTypeLabels[this.item().car.fuelType]);
  readonly params = computed(() => searchToParams(this.search()));
  readonly bookable = computed(() => this.item().car.isAvailable && !!this.item().fare?.isAvailable);
  readonly reason = computed(() => {
    const { car, fare } = this.item();
    if (!car.isAvailable) return 'Already booked for part of this time.';
    return fare?.unavailableReason ?? 'Can’t be booked online right now.';
  });
}
