import { CommonModule } from '@angular/common';
import { Component, Input, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TripSummary, UpcomingSlot } from '../../core/models/trip.model';
import { ImageUrlPipe } from '../pipes/image-url.pipe';

@Component({
  selector: 'app-trip-card',
  standalone: true,
  imports: [CommonModule, RouterLink, ImageUrlPipe],
  template: `
    <a class="tcard" [routerLink]="['/trips', trip.tripId]">
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
          <div class="cardots">
            @for (p of photoUrls; track $index) {
              <span class="dot" [class.on]="$index === activeIndex()"></span>
            }
          </div>
        }

        @if (seatsLabel) {
          <span class="seatpill" [class.low]="seatState === 'low'" [class.soldout]="seatState === 'soldout'">
            <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor"><path d="M12 2c1 3-2 4-2 7a3 3 0 0 0 6 0c1.5 1.5 2 3.5 2 5.5A6.5 6.5 0 0 1 5 14.5C5 9 9 6 12 2z"></path></svg>
            {{ seatsLabel }}
          </span>
        }

        @if (shortDuration) {
          <span class="durbadge mobiletab-only">{{ shortDuration }}</span>
        }

        @if (showAdvanceBadge) {
          <div class="pricestar mobiletab-only">
            <span class="pricestar-top">Book for</span>
            <span class="pricestar-amt">₹{{ advanceAmount }}</span>
          </div>
        }

        <div class="heroinfo mobiletab-only">
          <h3>{{ trip.title }}</h3>
          @if (placesLine) {
            <div class="herometa">
              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M12 21s-6.5-5.9-6.5-11A6.5 6.5 0 0 1 18.5 10c0 5.1-6.5 11-6.5 11z"></path><circle cx="12" cy="10" r="2.3"></circle></svg>
              <span>{{ placesLine }}</span>
            </div>
          }
        </div>
      </div>
      @if (rollingSlots.length > 0) {
        <!-- Information only: the list is rendered twice and the track slides left by exactly one
             copy (-50%), so the loop restarts on an identical frame with no visible jump. -->
        <div class="datestrip" aria-label="Upcoming departures">
          <div class="datestrip-track" [style.animation-duration.s]="rollingSlots.length * 3.2">
            @for (copy of [0, 1]; track copy) {
              @for (s of rollingSlots; track $index) {
                @let isDup = copy === 1 || $index >= trip.upcomingSlots.length;
                <span class="datepill" [class]="'tone' + ($index % 5)" [class.dup]="isDup" [attr.aria-hidden]="isDup ? 'true' : null">
                  <b>{{ s.startDate | date:'d MMM' | uppercase }}</b>
                  <small>{{ s.availableSeats === 0 ? 'Sold out' : s.availableSeats + (s.availableSeats === 1 ? ' seat' : ' seats') }}</small>
                </span>
              }
            }
          </div>
        </div>
      }
      <div class="body">
        <div class="titlerow desktop-only">
          <h3>{{ trip.title }}</h3>
          @if (trip.nextSlotStartDate) {
            <span class="badge ok" style="flex-shrink:0">{{ shortDuration }}</span>
          } @else {
            <span class="mut" style="font-size:11px;white-space:nowrap">Dates coming soon</span>
          }
        </div>

        @if (trip.nextSlotStartDate) {
          <div class="metaline desktop-only">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="18" rx="2"></rect><path d="M16 2v4M8 2v4M3 10h18"></path></svg>
            <span>{{ trip.nextSlotStartDate | date:'d MMM' }} → {{ trip.nextSlotEndDate | date:'d MMM y' }}</span>
          </div>
        }

        @if (placesLine) {
          <div class="metaline desktop-only">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M12 21s-6.5-5.9-6.5-11A6.5 6.5 0 0 1 18.5 10c0 5.1-6.5 11-6.5 11z"></path><circle cx="12" cy="10" r="2.3"></circle></svg>
            <span>{{ placesLine }}</span>
          </div>
        }

        @if (features.length > 0) {
          <div class="hr"></div>
          <div class="featrow">
            @for (f of features; track f.key) {
              @let parts = splitLabel(f.label);
              <div class="fi">
                @switch (f.key) {
                  @case ('stay') {
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 18v-7a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v7"></path><path d="M3 18h18M3 14h18"></path><path d="M6 11V7a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2"></path></svg>
                  }
                  @case ('breakfast') {
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M3 2v7c0 1.1.9 2 2 2h4a2 2 0 0 0 2-2V2"></path><path d="M7 2v20"></path><path d="M21 15V2a5 5 0 0 0-5 5v6c0 1.1.9 2 2 2h3Zm0 0v7"></path></svg>
                  }
                  @case ('dinner') {
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path></svg>
                  }
                  @case ('coordinator') {
                    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"></path><circle cx="9" cy="7" r="4"></circle><polyline points="17 11 19 13 23 9"></polyline></svg>
                  }
                }
                <div class="fi-label">
                  <b>{{ parts.main }}</b>
                  <span class="fi-sub">{{ parts.sub }}</span>
                </div>
              </div>
            }
          </div>
        }

        @if (showAdvanceBadge) {
          <div class="dealbanner desktop-only">
            <div class="headline">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M20.59 13.41 11 3.83A2 2 0 0 0 9.57 3H4a1 1 0 0 0-1 1v5.57a2 2 0 0 0 .83 1.42l9.59 9.58a2 2 0 0 0 2.83 0l4.34-4.34a2 2 0 0 0 0-2.82z"></path><circle cx="7.5" cy="7.5" r="1.2" fill="currentColor" stroke="none"></circle></svg>
              <b>Book this package @ ₹{{ advanceAmount }}</b>
            </div>
            <div class="sub">Confirm your seat now!</div>
          </div>
        }

        <div class="pricerow">
          <div class="price"><b>₹{{ trip.amountPerPerson | number:'1.0-0' }}</b><small>/ person</small></div>
          <span class="btn sm">Book Now →</span>
        </div>
      </div>
    </a>
  `,
})
export class TripCardComponent implements OnInit, OnDestroy {
  @Input({ required: true }) trip!: TripSummary;

  readonly activeIndex = signal(0);
  readonly imgFailed = signal(false);
  private touchStartX = 0;
  private carouselTimer?: ReturnType<typeof setInterval>;

  ngOnInit(): void {
    if (this.photoUrls.length > 1) {
      this.carouselTimer = setInterval(() => {
        const len = this.photoUrls.length;
        this.activeIndex.update((i) => (i + 1) % len);
      }, 3500);
    }
  }

  ngOnDestroy(): void {
    if (this.carouselTimer) {
      clearInterval(this.carouselTimer);
    }
  }

  get photoUrls(): string[] {
    if (this.trip.photos?.length) return this.trip.photos.map((p) => p.imageUrl);
    return this.trip.coverImageUrl ? [this.trip.coverImageUrl] : [];
  }

  get seatsLabel(): string | null {
    const seats = this.trip.nextSlotAvailableSeats;
    if (seats === null) return null;
    if (seats === 0) return 'Sold out';
    if (seats <= 3) return `Only ${seats} seat${seats === 1 ? '' : 's'} left`;
    return `${seats} seats left`;
  }

  get seatState(): 'normal' | 'low' | 'soldout' {
    const seats = this.trip.nextSlotAvailableSeats;
    if (seats === null) return 'normal';
    if (seats === 0) return 'soldout';
    if (seats <= 5) return 'low';
    return 'normal';
  }

  // One copy of the strip must be at least as wide as the card, otherwise a gap shows at the right
  // edge before the second copy arrives. With only a few departures, repeat them until one copy
  // holds enough pills to fill the card width.
  private static readonly MinPillsPerCopy = 6;
  private rollingSource?: UpcomingSlot[];
  private rollingCache: UpcomingSlot[] = [];

  get rollingSlots(): UpcomingSlot[] {
    const slots = this.trip.upcomingSlots ?? [];
    if (slots !== this.rollingSource) {
      this.rollingSource = slots;
      const repeats = slots.length === 0 ? 0 : Math.ceil(TripCardComponent.MinPillsPerCopy / slots.length);
      this.rollingCache = Array.from({ length: repeats }, () => slots).flat();
    }
    return this.rollingCache;
  }

  // No per-trip advance-booking amount exists on TripSummary yet; this mirrors
  // the flat ₹99 offer the dealbanner already showed unconditionally before this change.
  readonly advanceAmount = 99;
  readonly showAdvanceBadge = true;

  get shortDuration(): string {
    if (!this.trip.nextSlotStartDate || !this.trip.nextSlotEndDate) return '';
    const start = new Date(this.trip.nextSlotStartDate);
    const end = new Date(this.trip.nextSlotEndDate);
    const days = Math.round((end.getTime() - start.getTime()) / 86400000) + 1;
    const nights = days - 1;
    return `${days}D/${nights}N`;
  }

  get placesLine(): string {
    const names = this.trip.highlightPlaceNames ?? [];
    if (names.length === 0) return '';
    const shown = names.slice(0, 3).join(' · ');
    return names.length > 3 ? `${shown} · More` : shown;
  }

  get features(): { key: 'stay' | 'breakfast' | 'dinner' | 'coordinator'; label: string }[] {
    const inc = this.trip.inclusions;
    const items: { key: 'stay' | 'breakfast' | 'dinner' | 'coordinator'; label: string }[] = [];
    if (inc?.stay) items.push({ key: 'stay', label: 'Twin Sharing Room' });
    if (inc?.breakfast) items.push({ key: 'breakfast', label: 'Breakfast Included' });
    if (inc?.dinner) items.push({ key: 'dinner', label: 'Dinner Included' });
    if (inc?.coordinator) items.push({ key: 'coordinator', label: 'Trip Coordinator' });
    return items;
  }

  // Renders as two lines: everything but the last word (bold), then the last word (muted).
  splitLabel(label: string): { main: string; sub: string } {
    const words = label.trim().split(' ');
    const sub = words.pop() ?? '';
    return { main: words.join(' '), sub };
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
