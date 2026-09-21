import { CommonModule } from '@angular/common';
import { Component, Input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TripSummary } from '../../core/models/trip.model';
import { ImageUrlPipe } from '../pipes/image-url.pipe';

@Component({
  selector: 'app-trip-card',
  standalone: true,
  imports: [CommonModule, RouterLink, ImageUrlPipe],
  template: `
    <article class="tcard">
      <div class="shot" (touchstart)="onTouchStart($event)" (touchend)="onTouchEnd($event)">
        @if (photoUrls.length > 0 && !imgFailed()) {
          <img [src]="photoUrls[activeIndex()] | imageUrl" alt="{{ trip.title }}" (error)="imgFailed.set(true)" />
        } @else {
          <div class="noshot">
            <svg width="30" height="30" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
              <path d="M3 18l5.5-7 4 5 2.5-3 6 5"></path>
              <circle cx="8" cy="8" r="2"></circle>
            </svg>
          </div>
        }

        @if (photoUrls.length > 1 && !imgFailed()) {
          <button type="button" class="carw prev" (click)="prev($event)" aria-label="Previous photo">‹</button>
          <button type="button" class="carw next" (click)="next($event)" aria-label="Next photo">›</button>
          <div class="cardots">
            @for (p of photoUrls; track $index) {
              <span class="dot" [class.on]="$index === activeIndex()"></span>
            }
          </div>
        }

        @if (seatsLabel) {
          <span class="seatpill">{{ seatsLabel }}</span>
        }
      </div>
      <div class="body">
        <h3>{{ trip.title }}</h3>
        <div class="meta">
          {{ trip.durationLabel || 'Dates coming soon' }}
          @if (trip.nextSlotStartDate) {
            · {{ trip.nextSlotStartDate | date:'d MMM' }} → {{ trip.nextSlotEndDate | date:'d MMM y' }}
          }
        </div>
        <div class="row sp">
          <div class="price"><b>₹{{ trip.amountPerPerson | number:'1.0-0' }}</b><small>per person</small></div>
          <a class="btn sm" [routerLink]="['/trips', trip.tripId]">View Full Itinerary</a>
        </div>
      </div>
    </article>
  `,
})
export class TripCardComponent {
  @Input({ required: true }) trip!: TripSummary;

  readonly activeIndex = signal(0);
  readonly imgFailed = signal(false);
  private touchStartX = 0;

  get photoUrls(): string[] {
    if (this.trip.photos?.length) return this.trip.photos.map((p) => p.imageUrl);
    return this.trip.coverImageUrl ? [this.trip.coverImageUrl] : [];
  }

  get seatsLabel(): string | null {
    if (this.trip.nextSlotAvailableSeats === null) return null;
    return this.trip.nextSlotAvailableSeats === 0 ? 'Sold out' : `${this.trip.nextSlotAvailableSeats} seats left`;
  }

  prev(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const len = this.photoUrls.length;
    this.activeIndex.update((i) => (i - 1 + len) % len);
  }

  next(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const len = this.photoUrls.length;
    this.activeIndex.update((i) => (i + 1) % len);
  }

  onTouchStart(event: TouchEvent): void {
    this.touchStartX = event.touches[0].clientX;
  }

  onTouchEnd(event: TouchEvent): void {
    const delta = event.changedTouches[0].clientX - this.touchStartX;
    if (Math.abs(delta) < 40 || this.photoUrls.length < 2) return;
    if (delta < 0) this.next(event);
    else this.prev(event);
  }
}
