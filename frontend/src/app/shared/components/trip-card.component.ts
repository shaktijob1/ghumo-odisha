import { CommonModule } from '@angular/common';
import { Component, Input, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TripSummary, UpcomingSlot } from '../../core/models/trip.model';
import { ImageUrlPipe } from '../pipes/image-url.pipe';
import { tripPath } from '../utils/trip-path';

@Component({
  selector: 'app-trip-card',
  standalone: true,
  imports: [CommonModule, RouterLink, ImageUrlPipe],
  template: `
    <a class="tcard" [class.dash]="showNextDeparture" [routerLink]="link">
      <div class="shot" (touchstart)="onTouchStart($event)" (touchend)="onTouchEnd($event)">
        @if (photoUrls.length > 0 && !imgFailed()) {
          <img [src]="photoUrls[activeIndex()] | imageUrl" alt="{{ trip.title }}" loading="lazy" decoding="async" (error)="imgFailed.set(true)" />
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

        @if (shortDuration) {
          <span class="durbadge mobiletab-only">{{ shortDuration }}</span>
        }

        @if (showAdvanceBadge) {
          <div class="pricestar mobiletab-only">
            <span class="pricestar-top">Book for</span>
            <span class="pricestar-amt">₹{{ advanceAmount }}</span>
          </div>
        }

        @if (soldOut) {
          <span class="bookedribbon">Fully booked</span>
        }

        <div class="heroinfo mobiletab-only">
          <h3>{{ trip.title }}</h3>
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
                  <small>{{ s.availableSeats === 0 ? 'Booked' : s.availableSeats + (s.availableSeats === 1 ? ' seat' : ' seats') }}</small>
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

        @if (showNextDeparture) {
          @if (trip.nextSlotStartDate) {
            <div class="nextdep">
              <div class="nd-cal" aria-hidden="true">
                <span class="nd-mon">{{ trip.nextSlotStartDate | date:'MMM' | uppercase }}</span>
                <span class="nd-day">{{ trip.nextSlotStartDate | date:'d' }}</span>
              </div>
              <div class="nd-info">
                <span class="nd-lbl">Next departure</span>
                <b class="nd-range">
                  {{ trip.nextSlotStartDate | date:'EEE, d MMM' }}
                  @if (trip.nextSlotEndDate) { – {{ trip.nextSlotEndDate | date:'EEE, d MMM' }} }
                </b>
                <span class="nd-meta">
                  @if (departsIn) { <span>{{ departsIn }}</span> }
                </span>
              </div>
            </div>
          } @else {
            <div class="nextdep empty">
              <div class="nd-info">
                <span class="nd-lbl">Next departure</span>
                <b class="nd-range">Dates coming soon</b>
              </div>
            </div>
          }
          <!-- Next few departure start dates as mini calendars — only shown in the phone layout of
               the home page's Upcoming Adventures (styles.css), in place of the rolling strip. -->
          @if (departureChips.length > 0) {
            <div class="depchips">
              <span class="depchips-lbl">Next departures</span>
              <!-- The chips always share the full row; with fewer than four they're wider, so they
                   spell out the month and add the weekday (data-count drives that in styles.css). -->
              <div class="depchips-row" [attr.data-count]="departureChips.length">
                @for (s of departureChips; track s.startDate) {
                  <span class="dchip" [class.full]="s.availableSeats === 0" [attr.title]="s.availableSeats === 0 ? 'Booked' : null" [attr.aria-label]="s.availableSeats === 0 ? 'Booked' : null">
                    <span class="dchip-mon"><span class="m-short">{{ s.startDate | date:'MMM' | uppercase }}</span><span class="m-long">{{ s.startDate | date:'MMMM' | uppercase }}</span></span>
                    <span class="dchip-day">{{ s.startDate | date:'d' }}</span>
                    <span class="dchip-wd">{{ s.startDate | date:'EEEE' }}</span>
                    @if (s.availableSeats === 0) {
                      <!-- Booked date: a rubber stamp drops onto the chip. -->
                      <span class="dchip-stamp" aria-hidden="true">Booked</span>
                    }
                    @if (s.availableSeats > 0) {
                      <!-- Seats left on this date: a blinking red tag on the box's top-right corner. -->
                      <span class="dchip-seats">
                        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 1.6l3.05 6.3 6.95.95-5.05 4.85 1.25 6.9L12 17.3l-6.2 3.3 1.25-6.9L2 8.85l6.95-.95z"></path></svg>
                        <span><b>{{ s.availableSeats }}</b><span class="dchip-seats-w"> {{ s.availableSeats === 1 ? 'seat' : 'seats' }}</span> left</span>
                      </span>
                    }
                  </span>
                }
              </div>
            </div>
          }
        } @else if (features.length > 0) {
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
          @if (soldOut) {
            <span class="btn sm soldout" aria-disabled="true">Sold out</span>
          } @else {
            <span class="btn sm">Book Now</span>
          }
        </div>
      </div>
    </a>
  `,
  styles: `
    /* ============ Trip card: booked dates ============
       A booked date gets a red rubber stamp that drops onto its chip; when every date on the card is
       booked, a ribbon on the photo and a panel offering the next open dates (naming a searched month). */
    .dchip-stamp { position: absolute; left: 50%; top: calc(100% - 11px); z-index: 2; padding: 3px 7px; border: 2px solid var(--danger); border-radius: 6px; background: rgba(255,255,255,.88); color: var(--danger); font-size: 11px; font-weight: 800; letter-spacing: .12em; text-transform: uppercase; line-height: 1.2; white-space: nowrap; pointer-events: none; transform: translate(-50%, -50%) rotate(-14deg); box-shadow: 0 0 0 2px rgba(192,72,58,.12); animation: dchip-stamp-in .55s cubic-bezier(.2,.9,.3,1.25) both; }
    /* The stamp sits across the chip's bottom edge (no extra height), so the date above stays readable;
       four chips to a row are narrow, so their stamp is smaller. */
    .depchips-row[data-count="4"] .dchip-stamp { padding: 2px 4px; border-width: 1.5px; font-size: 9px; letter-spacing: .05em; }
    .dchip:nth-child(2) .dchip-stamp { animation-delay: .12s; }
    .dchip:nth-child(3) .dchip-stamp { animation-delay: .24s; }
    .dchip:nth-child(4) .dchip-stamp { animation-delay: .36s; }
    @keyframes dchip-stamp-in {
      0% { opacity: 0; transform: translate(-50%, -50%) rotate(-14deg) scale(2.4); }
      60% { opacity: 1; transform: translate(-50%, -50%) rotate(-14deg) scale(.92); }
      100% { opacity: 1; transform: translate(-50%, -50%) rotate(-14deg) scale(1); }
    }
    .tcard .bookedribbon { position: absolute; top: 10px; left: 10px; z-index: 2; padding: 5px 12px; border-radius: 999px; background: var(--danger); color: #fff; font-size: 12px; font-weight: 700; box-shadow: 0 4px 12px rgba(192,72,58,.35); }
    @media (prefers-reduced-motion: reduce) { .dchip-stamp { animation: none; } }
  `,
})
export class TripCardComponent implements OnInit, OnDestroy {
  @Input({ required: true }) trip!: TripSummary;
  // Dashboard and destination pages: shows the next departure date in place of the inclusions row.
  @Input() showNextDeparture = false;

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

  /** Every date on the card is booked (sold out or closed online). */
  get allBooked(): boolean {
    const slots = this.trip.upcomingSlots ?? [];
    return slots.length > 0 && slots.every((s) => s.availableSeats === 0);
  }

  /** Every date on the card is booked and no later date is open either — the card can't be booked. */
  get soldOut(): boolean {
    return this.allBooked && (this.trip.nextOpenSlots ?? []).length === 0;
  }

  /**
   * Up to four departures (the API already limits them to a searched month, if any). When all of
   * them are booked, the next open dates (up to two) take the last places beside them.
   */
  get departureChips(): UpcomingSlot[] {
    const slots = this.trip.upcomingSlots ?? [];
    const open = this.allBooked ? (this.trip.nextOpenSlots ?? []).slice(0, 2) : [];
    return [...slots.slice(0, 4 - open.length), ...open];
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

  // Calendar days from today (local time) to the next departure.
  get departsIn(): string {
    if (!this.trip.nextSlotStartDate) return '';
    const start = new Date(this.trip.nextSlotStartDate);
    const today = new Date();
    const startDay = Date.UTC(start.getFullYear(), start.getMonth(), start.getDate());
    const todayDay = Date.UTC(today.getFullYear(), today.getMonth(), today.getDate());
    const days = Math.round((startDay - todayDay) / 86400000);
    if (days < 0) return '';
    if (days === 0) return 'Today';
    if (days === 1) return 'Tomorrow';
    return `In ${days} days`;
  }

  get link(): string {
    return tripPath(this.trip.tripId, this.trip.title);
  }

  get features(): { key: 'stay' | 'breakfast' | 'dinner' | 'coordinator'; label: string }[] {
    const inc = this.trip.inclusions;
    const items: { key: 'stay' | 'breakfast' | 'dinner' | 'coordinator'; label: string }[] = [];
    if (inc?.stay) items.push({ key: 'stay', label: 'AC Room' });
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
