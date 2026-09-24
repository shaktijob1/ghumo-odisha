import { CommonModule } from '@angular/common';
import { Component, ElementRef, NgZone, OnDestroy, OnInit, computed, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { PublicTripService } from '../../core/services/public-trip.service';
import { PublicDestinationService } from '../../core/services/public-destination.service';
import { ContactService } from '../../core/services/contact.service';
import { HeroService } from '../../core/services/hero.service';
import { TripSummary } from '../../core/models/trip.model';
import { DestinationSummary } from '../../core/models/destination.model';
import { TripCardComponent } from '../../shared/components/trip-card.component';
import { DestinationCardComponent } from '../../shared/components/destination-card.component';
import { FeatureCardComponent, FeatureIcon } from '../../shared/components/feature-card.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';

type LoadState = 'loading' | 'ready' | 'error';

interface Feature {
  icon: FeatureIcon;
  title: string;
  description: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, TripCardComponent, DestinationCardComponent, FeatureCardComponent, ImageUrlPipe],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit, OnDestroy {
  private readonly tripService = inject(PublicTripService);
  private readonly destinationService = inject(PublicDestinationService);
  private readonly contactService = inject(ContactService);
  private readonly heroPhotoService = inject(HeroService);
  private readonly ngZone = inject(NgZone);

  readonly contact = this.contactService.get();
  private readonly siteHeroPhoto = this.heroPhotoService.get();
  private readonly siteHeroPhotoFailed = signal(false);

  readonly state = signal<LoadState>('loading');
  private readonly trips = signal<TripSummary[]>([]);

  readonly destinationsState = signal<LoadState>('loading');
  readonly destinations = signal<DestinationSummary[]>([]);

  // --- Mobile "Trending Odisha Destinations" carousel (synced hero + 3-up centered mini-cards) ---
  private readonly GAP_PX = 10;
  private readonly AUTOPLAY_MS = 4000;
  private readonly SWIPE_THRESHOLD_RATIO = 0.24;
  private readonly FLICK_VELOCITY = 0.5; // px/ms
  private readonly HERO_TEXT_DELAY_MS = 170;

  private readonly carouselTrack = viewChild<ElementRef<HTMLElement>>('carouselTrack');
  private readonly containerWidth = signal(0);
  private resizeObserver: ResizeObserver | null = null;

  readonly activeIndex = signal(0);
  readonly activeDestination = computed(() => {
    const list = this.destinations();
    return list[this.activeIndex()] ?? list[0] ?? null;
  });

  // Hero text (title/price/tags/badges) lags the image by a short stagger so the crossfade reads
  // as one deliberate transition rather than an instant text swap.
  readonly displayedDestination = signal<DestinationSummary | null>(null);
  readonly displayedIndex = signal(0);
  readonly heroTextFading = signal(false);
  private heroTextTimer: ReturnType<typeof setTimeout> | null = null;

  readonly dragging = signal(false);
  private dragStartX = 0;
  private dragMoved = false;
  private pointerId: number | null = null;
  private lastMoveX = 0;
  private lastMoveTime = 0;
  private velocity = 0;
  private pendingDx = 0;
  private dragCardEls: HTMLElement[] = [];
  private dragBaseOffsets: number[] = [];
  private dragCleanup: (() => void) | null = null;

  readonly reducedMotion =
    typeof window !== 'undefined' && !!window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

  private readonly mobileMql = typeof window !== 'undefined' ? window.matchMedia('(max-width: 640px)') : null;
  private autoplayHandle: ReturnType<typeof setInterval> | null = null;

  private readonly onMqlChange = (e: MediaQueryListEvent): void => {
    if (e.matches) this.startAutoplay();
    else this.stopAutoplay();
  };

  private readonly onVisibilityChange = (): void => {
    if (document.hidden) this.stopAutoplay();
    else this.startAutoplay();
  };

  constructor() {
    effect(() => {
      const el = this.carouselTrack()?.nativeElement;
      this.resizeObserver?.disconnect();
      this.resizeObserver = null;
      if (el) {
        this.containerWidth.set(el.clientWidth);
        this.resizeObserver = new ResizeObserver((entries) => {
          this.containerWidth.set(entries[0]?.contentRect.width ?? 0);
        });
        this.resizeObserver.observe(el);
      }
    });

    let initialized = false;
    effect(() => {
      const dest = this.activeDestination();
      const idx = this.activeIndex();
      if (!initialized) {
        if (dest === null) return;
        initialized = true;
        this.displayedDestination.set(dest);
        this.displayedIndex.set(idx);
        return;
      }
      this.heroTextFading.set(true);
      if (this.heroTextTimer) clearTimeout(this.heroTextTimer);
      this.heroTextTimer = setTimeout(() => {
        this.displayedDestination.set(dest);
        this.displayedIndex.set(idx);
        this.heroTextFading.set(false);
      }, this.HERO_TEXT_DELAY_MS);
    });
  }

  readonly locationSearch = signal('');
  readonly fromDate = signal('');
  readonly toDate = signal('');

  // Distinct destination names across every loaded trip — feeds both the "All" + per-location
  // pill row under Upcoming Trips and the autocomplete dropdown on the hero search field.
  readonly distinctLocations = computed(() => {
    const names = new Set<string>();
    for (const t of this.trips()) {
      for (const name of t.destinationNames) names.add(name);
    }
    return Array.from(names).sort((a, b) => a.localeCompare(b));
  });

  readonly selectedLocation = signal<string | null>(null);

  readonly upcomingTrips = computed(() => {
    const location = this.selectedLocation();
    return this.trips().filter((t) => this.hasUpcomingSlot(t) && (!location || t.destinationNames.includes(location)));
  });

  private static readonly TRIPS_PAGE_SIZE = 5;
  readonly visibleTripsCount = signal(DashboardComponent.TRIPS_PAGE_SIZE);
  readonly visibleUpcomingTrips = computed(() => this.upcomingTrips().slice(0, this.visibleTripsCount()));

  showMoreTrips(): void {
    this.visibleTripsCount.update((n) => n + DashboardComponent.TRIPS_PAGE_SIZE);
  }

  selectLocation(location: string | null): void {
    this.selectedLocation.set(location);
    this.visibleTripsCount.set(DashboardComponent.TRIPS_PAGE_SIZE);
  }

  // --- Hero search location autocomplete ---
  readonly showLocationSuggestions = signal(false);
  readonly locationSuggestions = computed(() => {
    const query = this.locationSearch().trim().toLowerCase();
    const list = this.distinctLocations();
    return query ? list.filter((l) => l.toLowerCase().includes(query)) : list;
  });

  onLocationFieldFocus(): void {
    this.showLocationSuggestions.set(true);
  }

  onLocationFieldBlur(): void {
    // Delayed so a click/mousedown on a suggestion below still registers before it disappears.
    setTimeout(() => this.showLocationSuggestions.set(false), 150);
  }

  selectLocationSuggestion(location: string): void {
    // Deliberately does not fill locationSearch — that's the free-text/backend search box and
    // would surface a redundant "active filter" chip duplicating the location pill this already sets.
    this.locationSearch.set('');
    this.showLocationSuggestions.set(false);
    this.selectLocation(location);
    this.jumpToUpcomingTrips();
  }

  // Briefly flashes the Upcoming Trips section so it's obvious where a hero-search jumped to,
  // whether that came from picking a location suggestion or hitting Search directly.
  readonly highlightUpcoming = signal(false);
  private highlightTimer?: ReturnType<typeof setTimeout>;

  private jumpToUpcomingTrips(): void {
    this.scrollTo('upcoming-trips');
    this.highlightUpcoming.set(true);
    if (this.highlightTimer) clearTimeout(this.highlightTimer);
    this.highlightTimer = setTimeout(() => this.highlightUpcoming.set(false), 1800);
  }

  private readonly heroImageCandidates = computed(() =>
    this.trips()
      .map((t) => t.coverImageUrl)
      .filter((url): url is string => !!url),
  );
  private readonly heroImageIndex = signal(0);

  // A dedicated, admin-uploaded hero photo (see AdminHeroController) takes priority over cycling
  // trip cover photos; if it fails to load, fall back to the trip-photo carousel as before.
  readonly heroImage = computed(() => {
    const dedicated = this.siteHeroPhoto();
    if (dedicated && !this.siteHeroPhotoFailed()) return dedicated;
    return this.heroImageCandidates()[this.heroImageIndex()] ?? null;
  });

  onHeroImageError(): void {
    if (this.siteHeroPhoto() && !this.siteHeroPhotoFailed()) {
      this.siteHeroPhotoFailed.set(true);
      return;
    }
    this.heroImageIndex.update((i) => i + 1);
  }

  readonly features: Feature[] = [
    { icon: 'compass', title: 'Curated Trips', description: 'Thoughtfully planned travel experiences.' },
    { icon: 'users', title: 'Small Group Experiences', description: 'Travel with a comfortable group and meet new people.' },
    { icon: 'map-pin', title: 'Local Experiences', description: 'Explore destinations beyond the usual tourist routes.' },
    { icon: 'luggage', title: 'Comfortable Travel', description: 'Planned transportation and accommodation where included.' },
    { icon: 'chat', title: 'Simple Booking', description: 'Easy booking through WhatsApp and direct communication.' },
    { icon: 'life-buoy', title: 'Personal Trip Support', description: 'Get in touch with Ghumo Odisha organizers when you need help.' },
  ];

  ngOnInit(): void {
    this.loadTrips();
    this.loadDestinations();
    this.mobileMql?.addEventListener('change', this.onMqlChange);
    document.addEventListener('visibilitychange', this.onVisibilityChange);
  }

  ngOnDestroy(): void {
    this.stopAutoplay();
    this.mobileMql?.removeEventListener('change', this.onMqlChange);
    document.removeEventListener('visibilitychange', this.onVisibilityChange);
    this.resizeObserver?.disconnect();
    if (this.heroTextTimer) clearTimeout(this.heroTextTimer);
    if (this.highlightTimer) clearTimeout(this.highlightTimer);
    this.dragCleanup?.();
  }

  loadDestinations(): void {
    this.destinationsState.set('loading');
    this.destinationService.getTrendingDestinations().subscribe({
      next: (d) => {
        this.destinations.set(d);
        this.destinationsState.set('ready');
        this.activeIndex.set(0);
        this.startAutoplay();
      },
      error: () => this.destinationsState.set('error'),
    });
  }

  // --- Carousel behavior (mobile only; desktop `.dscroll` grid is untouched) ---

  private circularOffset(i: number): number {
    const n = this.destinations().length;
    if (n === 0) return 0;
    let raw = (i - this.activeIndex()) % n;
    if (raw > n / 2) raw -= n;
    if (raw < -n / 2) raw += n;
    return raw;
  }

  // Exactly 3 equal cards fit the row: width = (100% - 2*gap) / 3 (mirrors the CSS `.mcard` width).
  private cardWidthPx(): number {
    const w = this.containerWidth();
    return w > 0 ? (w - 2 * this.GAP_PX) / 3 : 0;
  }

  private slotStepPx(): number {
    const cw = this.cardWidthPx();
    return cw > 0 ? cw + this.GAP_PX : 0;
  }

  tagsFor(d: DestinationSummary | null): string[] {
    if (!d?.tagline) return [];
    return d.tagline
      .split(/[•|,]/)
      .map((s) => s.trim())
      .filter(Boolean)
      .slice(0, 3);
  }

  tagIcon(tag: string): 'mountain' | 'tree' | 'sun' | 'water' | 'landmark' | 'compass' {
    const t = tag.toLowerCase();
    if (t.includes('water') || t.includes('beach') || t.includes('sea') || t.includes('river') || t.includes('lake')) return 'water';
    if (t.includes('hill') || t.includes('mountain') || t.includes('trek') || t.includes('adventure')) return 'mountain';
    if (t.includes('forest') || t.includes('nature') || t.includes('wildlife') || t.includes('green')) return 'tree';
    if (t.includes('temple') || t.includes('heritage') || t.includes('culture') || t.includes('history') || t.includes('fort')) return 'landmark';
    if (t.includes('sun') || t.includes('sunrise') || t.includes('sunset')) return 'sun';
    return 'compass';
  }

  offsetPx(i: number): number {
    return this.circularOffset(i) * this.slotStepPx();
  }

  private next(): void {
    const n = this.destinations().length;
    if (n === 0) return;
    this.activeIndex.update((i) => (i + 1) % n);
  }

  private prev(): void {
    const n = this.destinations().length;
    if (n === 0) return;
    this.activeIndex.update((i) => (i - 1 + n) % n);
  }

  goTo(i: number): void {
    const n = this.destinations().length;
    if (n === 0) return;
    this.activeIndex.set(((i % n) + n) % n);
    this.resetAutoplay();
  }

  private startAutoplay(): void {
    this.stopAutoplay();
    if (!this.mobileMql?.matches || this.destinations().length <= 1) return;
    this.autoplayHandle = setInterval(() => this.next(), this.AUTOPLAY_MS);
  }

  private stopAutoplay(): void {
    if (this.autoplayHandle !== null) {
      clearInterval(this.autoplayHandle);
      this.autoplayHandle = null;
    }
  }

  private resetAutoplay(): void {
    this.startAutoplay();
  }

  // Drag tracking runs entirely outside Angular's zone and writes the `--x` custom property
  // straight to the DOM per pointermove, so a swipe never triggers a full-page change-detection
  // pass (that zone.js CD storm — one full template re-check per move event — is what made the
  // drag feel rough). We only re-enter the zone once, on release, to commit the final state.
  onPointerDown(ev: PointerEvent): void {
    if (ev.pointerType === 'mouse' && ev.button !== 0) return;
    const trackEl = this.carouselTrack()?.nativeElement;
    if (!trackEl) return;

    this.pointerId = ev.pointerId;
    this.dragStartX = ev.clientX;
    this.dragMoved = false;
    this.dragging.set(true);
    this.lastMoveX = ev.clientX;
    this.lastMoveTime = performance.now();
    this.velocity = 0;
    this.pendingDx = 0;
    this.stopAutoplay();
    try {
      (ev.currentTarget as HTMLElement)?.setPointerCapture?.(ev.pointerId);
    } catch {
      // Some browsers/synthetic events reject capture for a pointer id that isn't "active" yet;
      // the drag still works fine via the window-level listeners below, so this is non-fatal.
    }

    this.dragCardEls = Array.from(trackEl.querySelectorAll<HTMLElement>('.mcard'));
    this.dragBaseOffsets = this.dragCardEls.map((_, i) => this.circularOffset(i) * this.slotStepPx());

    this.dragCleanup?.();
    this.ngZone.runOutsideAngular(() => {
      const onMove = (e: PointerEvent) => {
        if (e.pointerId !== this.pointerId) return;
        const dx = e.clientX - this.dragStartX;
        if (Math.abs(dx) > 4) this.dragMoved = true;
        const now = performance.now();
        const dt = now - this.lastMoveTime;
        if (dt > 0) this.velocity = (e.clientX - this.lastMoveX) / dt;
        this.lastMoveX = e.clientX;
        this.lastMoveTime = now;
        this.pendingDx = dx;
        for (let i = 0; i < this.dragCardEls.length; i++) {
          this.dragCardEls[i].style.setProperty('--x', `${this.dragBaseOffsets[i] + dx}px`);
        }
      };
      const onUp = (e: PointerEvent) => {
        if (e.pointerId !== this.pointerId) return;
        cleanup();
        this.ngZone.run(() => this.finishDrag(false));
      };
      const onCancel = (e: PointerEvent) => {
        if (e.pointerId !== this.pointerId) return;
        cleanup();
        this.ngZone.run(() => this.finishDrag(true));
      };
      const cleanup = () => {
        window.removeEventListener('pointermove', onMove);
        window.removeEventListener('pointerup', onUp);
        window.removeEventListener('pointercancel', onCancel);
        this.dragCleanup = null;
      };
      window.addEventListener('pointermove', onMove);
      window.addEventListener('pointerup', onUp);
      window.addEventListener('pointercancel', onCancel);
      this.dragCleanup = cleanup;
    });
  }

  private finishDrag(cancelled: boolean): void {
    this.dragging.set(false);
    this.pointerId = null;
    if (!cancelled) {
      const dx = this.pendingDx;
      const step = this.slotStepPx();
      const threshold = step > 0 ? step * this.SWIPE_THRESHOLD_RATIO : 40;
      if (dx <= -threshold || this.velocity <= -this.FLICK_VELOCITY) this.next();
      else if (dx >= threshold || this.velocity >= this.FLICK_VELOCITY) this.prev();
    }
    this.resetAutoplay();
  }

  onActiveCardClick(ev: MouseEvent): void {
    if (this.dragMoved) {
      ev.preventDefault();
      this.dragMoved = false;
    }
  }

  onSideCardClick(i: number): void {
    if (this.dragMoved) {
      this.dragMoved = false;
      return;
    }
    this.goTo(i);
  }

  loadTrips(): void {
    this.state.set('loading');
    this.tripService
      .getTrips(1, 50, {
        search: this.locationSearch().trim() || undefined,
        fromDate: this.fromDate() || undefined,
        toDate: this.toDate() || undefined,
      })
      .subscribe({
        next: (r) => {
          this.trips.set(r.items);
          this.visibleTripsCount.set(DashboardComponent.TRIPS_PAGE_SIZE);
          this.state.set('ready');
        },
        error: () => this.state.set('error'),
      });
  }

  applyFilters(): void {
    this.loadTrips();
    this.jumpToUpcomingTrips();
  }

  readonly hasActiveFilters = computed(() => !!this.locationSearch().trim() || !!this.fromDate() || !!this.toDate());

  clearLocationFilter(): void {
    this.locationSearch.set('');
    this.loadTrips();
  }

  clearDateFilter(): void {
    this.fromDate.set('');
    this.toDate.set('');
    this.loadTrips();
  }

  clearAllFilters(): void {
    this.locationSearch.set('');
    this.fromDate.set('');
    this.toDate.set('');
    this.loadTrips();
  }

  scrollTo(id: string): void {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  openWhatsApp(): void {
    const message = "Hi! I'd like to know more about your upcoming Odisha trips.";
    window.open(this.contactService.buildWhatsAppLink(message), '_blank');
  }

  private hasUpcomingSlot(trip: TripSummary): boolean {
    if (!trip.nextSlotStartDate) return false;
    const start = new Date(trip.nextSlotStartDate);
    const todayStart = new Date();
    todayStart.setHours(0, 0, 0, 0);
    return start >= todayStart;
  }
}
