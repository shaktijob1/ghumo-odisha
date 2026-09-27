import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, OnDestroy, OnInit, computed, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PublicTripService } from '../../core/services/public-trip.service';
import { PublicDestinationService } from '../../core/services/public-destination.service';
import { ContactService } from '../../core/services/contact.service';
import { HeroService } from '../../core/services/hero.service';
import { SearchLogService } from '../../core/services/search-log.service';
import { TripSummary } from '../../core/models/trip.model';
import { DestinationSummary } from '../../core/models/destination.model';
import { TripCardComponent } from '../../shared/components/trip-card.component';
import { DestinationCardComponent } from '../../shared/components/destination-card.component';
import { FeatureCardComponent, FeatureIcon } from '../../shared/components/feature-card.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';

type LoadState = 'loading' | 'ready' | 'error';

interface MonthOption {
  value: string; // "yyyy-MM"
  label: string; // "October" — the next 12 months, so a month name alone is unambiguous
  short: string; // "Oct" — shown in the field on phones
}

/** This month and the following ones, for the hero month picker. */
function buildMonthOptions(count: number): MonthOption[] {
  const now = new Date();
  return Array.from({ length: count }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() + i, 1);
    return {
      value: `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`,
      label: d.toLocaleDateString('en-IN', { month: 'long' }),
      short: d.toLocaleDateString('en-IN', { month: 'short' }),
    };
  });
}

/** "2026-10" → trips with a departure overlapping 1–31 Oct 2026. */
function monthRange(month: string): { fromDate: string; toDate: string } {
  const [year, mon] = month.split('-').map(Number);
  const lastDay = new Date(year, mon, 0).getDate();
  return { fromDate: `${month}-01`, toDate: `${month}-${String(lastDay).padStart(2, '0')}` };
}

interface Feature {
  icon: FeatureIcon;
  title: string;
  description: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, TripCardComponent, DestinationCardComponent, FeatureCardComponent, ImageUrlPipe],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit, OnDestroy {
  private readonly tripService = inject(PublicTripService);
  private readonly destinationService = inject(PublicDestinationService);
  private readonly contactService = inject(ContactService);
  private readonly heroPhotoService = inject(HeroService);
  private readonly searchLog = inject(SearchLogService);

  readonly contact = this.contactService.get();
  readonly orgPhotoFailed = signal(false);
  private readonly siteHeroPhoto = this.heroPhotoService.get();
  private readonly siteHeroPhotoFailed = signal(false);

  readonly state = signal<LoadState>('loading');
  private readonly trips = signal<TripSummary[]>([]);

  readonly destinationsState = signal<LoadState>('loading');
  readonly destinations = signal<DestinationSummary[]>([]);

  readonly reducedMotion =
    typeof window !== 'undefined' && !!window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

  // --- "Trending Odisha Destinations" row (all screen sizes): horizontal scroll + prev/next arrows ---
  private readonly trendScroll = viewChild<ElementRef<HTMLElement>>('trendScroll');
  readonly canScrollPrev = signal(false);
  readonly canScrollNext = signal(false);
  private readonly trendArrowSync = effect(() => {
    // Re-check once the card row renders (or the destinations change).
    this.destinations();
    if (this.trendScroll()) setTimeout(() => this.updateTrendArrows());
  });

  updateTrendArrows(): void {
    const el = this.trendScroll()?.nativeElement;
    if (!el) return;
    this.canScrollPrev.set(el.scrollLeft > 2);
    this.canScrollNext.set(el.scrollLeft + el.clientWidth < el.scrollWidth - 2);
  }

  /** Moves the row by roughly one visible "page" of cards. */
  scrollTrending(direction: 1 | -1): void {
    const el = this.trendScroll()?.nativeElement;
    if (!el) return;
    el.scrollBy({ left: direction * Math.max(el.clientWidth * 0.85, 140), behavior: this.reducedMotion ? 'auto' : 'smooth' });
  }

  // --- "Upcoming Trips" row: 4 cards per view (3 / 2 on narrower screens, ~1 on phones), arrows move 2 (1 on phones) ---
  private readonly tripScroll = viewChild<ElementRef<HTMLElement>>('tripScroll');
  readonly tripCanScrollPrev = signal(false);
  readonly tripCanScrollNext = signal(false);
  private readonly tripArrowSync = effect(() => {
    // A new trip list (load or location filter) starts the row from the beginning.
    this.upcomingTrips();
    const el = this.tripScroll()?.nativeElement;
    if (el) setTimeout(() => { el.scrollLeft = 0; this.updateTripArrows(); });
  });

  updateTripArrows(): void {
    const el = this.tripScroll()?.nativeElement;
    if (!el) return;
    this.tripCanScrollPrev.set(el.scrollLeft > 2);
    this.tripCanScrollNext.set(el.scrollLeft + el.clientWidth < el.scrollWidth - 2);
  }

  /**
   * Moves the row by 2 cards. The browser stops at the end of the row, so with only one card
   * left off-screen (e.g. 5 trips, 4 visible) a click moves just that one.
   */
  scrollTrips(direction: 1 | -1): void {
    const el = this.tripScroll()?.nativeElement;
    const card = el?.querySelector<HTMLElement>('.tslot');
    if (!el || !card) return;
    const gap = parseFloat(getComputedStyle(el).columnGap) || 0;
    const step = card.getBoundingClientRect().width + gap;
    // Phones show about one card per view, so move one at a time there.
    const cards = step * 2 <= el.clientWidth ? 2 : 1;
    el.scrollBy({ left: direction * cards * step, behavior: this.reducedMotion ? 'auto' : 'smooth' });
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    this.updateTrendArrows();
    this.updateTripArrows();
  }

  // --- Hero search: month picker + place text box with suggestions ---
  readonly monthOptions = buildMonthOptions(12);
  // Draft values in the hero search ('' = any); they apply only when Search is pressed.
  readonly searchMonth = signal('');
  readonly searchPlace = signal('');
  readonly searchMonthOption = computed(() => this.monthOptions.find((m) => m.value === this.searchMonth()) ?? null);
  readonly monthOpen = signal(false);
  readonly placeOpen = signal(false);
  private readonly placeInput = viewChild<ElementRef<HTMLInputElement>>('placeInput');

  // What is currently filtering Upcoming Trips: a month ("yyyy-MM") and/or free place text that
  // didn't match a known place exactly ('' = none). An exact place match uses selectedLocation.
  readonly appliedMonth = signal('');
  readonly appliedMonthLabel = computed(() => this.monthLabel(this.appliedMonth()));
  readonly placeQuery = signal('');

  // Every place seen while no month filter was applied, so the suggestions don't shrink
  // once a month search narrows the trip list.
  private readonly knownLocations = signal<string[]>([]);
  readonly placeOptions = computed(() => {
    const names = new Set(this.knownLocations());
    for (const d of this.destinations()) names.add(d.name);
    return Array.from(names).sort((a, b) => a.localeCompare(b));
  });

  // Typed text narrows the list; once it exactly matches a place, show everything again so the
  // visitor can switch without clearing the box first.
  readonly placeSuggestions = computed(() => {
    const query = this.searchPlace().trim().toLowerCase();
    const list = this.placeOptions();
    if (!query || list.some((p) => p.toLowerCase() === query)) return list;
    return list.filter((p) => p.toLowerCase().includes(query));
  });

  private monthLabel(value: string): string {
    return this.monthOptions.find((m) => m.value === value)?.label ?? '';
  }

  toggleMonth(): void {
    this.placeOpen.set(false);
    this.monthOpen.update((open) => !open);
  }

  pickMonth(value: string): void {
    this.searchMonth.set(this.searchMonth() === value ? '' : value);
    this.monthOpen.set(false);
  }

  /** A click anywhere in the Place section opens the text box and its suggestions. */
  openPlace(): void {
    this.monthOpen.set(false);
    this.placeOpen.set(true);
    this.placeInput()?.nativeElement.focus();
  }

  pickPlace(place: string): void {
    this.searchPlace.set(place);
    this.placeOpen.set(false);
  }

  clearSearchPlace(event: Event): void {
    event.stopPropagation();
    this.searchPlace.set('');
    this.openPlace();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as Element | null;
    if (!target?.closest('.hs-month')) this.monthOpen.set(false);
    if (!target?.closest('.hs-place')) this.placeOpen.set(false);
  }

  @HostListener('document:keydown.escape')
  closeSearchPopovers(): void {
    this.monthOpen.set(false);
    this.placeOpen.set(false);
  }

  readonly selectedLocation = signal<string | null>(null);

  // Distinct destination names across the loaded trips — the "All" + per-location pill row under
  // Upcoming Trips. Keeps the selected place visible even when a month search excludes it.
  readonly distinctLocations = computed(() => {
    const names = new Set<string>();
    for (const t of this.trips()) {
      for (const name of t.destinationNames) names.add(name);
    }
    const selected = this.selectedLocation();
    if (selected) names.add(selected);
    return Array.from(names).sort((a, b) => a.localeCompare(b));
  });

  readonly upcomingTrips = computed(() => {
    const location = this.selectedLocation();
    const query = this.placeQuery().trim().toLowerCase();
    return this.trips().filter(
      (t) =>
        this.hasUpcomingSlot(t) &&
        (!location || t.destinationNames.includes(location)) &&
        (!query || t.title.toLowerCase().includes(query) || t.destinationNames.some((n) => n.toLowerCase().includes(query))),
    );
  });

  selectLocation(location: string | null): void {
    this.selectedLocation.set(location);
    this.placeQuery.set('');
    // Keep the hero place box in step with the pill row.
    this.searchPlace.set(location ?? '');
  }

  // Briefly flashes the Upcoming Trips section so it's obvious where a hero-search jumped to.
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
  }

  ngOnDestroy(): void {
    if (this.highlightTimer) clearTimeout(this.highlightTimer);
  }

  loadDestinations(): void {
    this.destinationsState.set('loading');
    this.destinationService.getTrendingDestinations().subscribe({
      next: (d) => {
        this.destinations.set(d);
        this.destinationsState.set('ready');
      },
      error: () => this.destinationsState.set('error'),
    });
  }

  // Only the newest request may update the list, so quick back-to-back searches can't land out of order.
  private tripLoadSeq = 0;

  loadTrips(onLoaded?: () => void): void {
    const seq = ++this.tripLoadSeq;
    const month = this.appliedMonth();
    this.state.set('loading');
    this.tripService
      .getTrips(1, 50, month ? monthRange(month) : undefined)
      .subscribe({
        next: (r) => {
          if (seq !== this.tripLoadSeq) return;
          this.trips.set(r.items);
          if (!month) {
            const names = new Set(this.knownLocations());
            for (const t of r.items) for (const name of t.destinationNames) names.add(name);
            this.knownLocations.set(Array.from(names));
          }
          this.state.set('ready');
          onLoaded?.();
        },
        error: () => {
          if (seq === this.tripLoadSeq) this.state.set('error');
        },
      });
  }

  /** Hero "Search": applies month + place to Upcoming Trips and records the search for admins. */
  applyFilters(): void {
    this.closeSearchPopovers();
    const month = this.searchMonth();
    const text = this.searchPlace().trim();
    const exact = this.placeOptions().find((p) => p.toLowerCase() === text.toLowerCase()) ?? null;

    this.selectLocation(exact);
    if (!exact && text) {
      this.placeQuery.set(text);
      this.searchPlace.set(text);
    }
    this.appliedMonth.set(month);
    this.loadTrips(() => {
      if (month || text) {
        this.searchLog.record({ month: month || null, place: exact ?? (text || null), resultCount: this.upcomingTrips().length });
      }
    });
    this.jumpToUpcomingTrips();
  }

  readonly hasActiveFilters = computed(() => !!this.appliedMonth() || !!this.placeQuery());

  clearMonthFilter(): void {
    this.appliedMonth.set('');
    this.searchMonth.set('');
    this.loadTrips();
  }

  clearPlaceQuery(): void {
    this.placeQuery.set('');
    this.searchPlace.set('');
  }

  clearAllFilters(): void {
    this.selectLocation(null);
    this.clearMonthFilter();
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
