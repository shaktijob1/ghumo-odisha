import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, OnDestroy, OnInit, computed, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PublicTripService } from '../../core/services/public-trip.service';
import { PublicDestinationService } from '../../core/services/public-destination.service';
import { ContactService } from '../../core/services/contact.service';
import { HeroService } from '../../core/services/hero.service';
import { SearchLogService } from '../../core/services/search-log.service';
import { FeatureService } from '../../core/services/feature.service';
import { TripSummary } from '../../core/models/trip.model';
import { DestinationSummary } from '../../core/models/destination.model';
import { TripCardComponent } from '../../shared/components/trip-card.component';
import { DestinationCardComponent } from '../../shared/components/destination-card.component';
import { FeatureCardComponent, FeatureIcon } from '../../shared/components/feature-card.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { DatePickerComponent } from '../../shared/components/date-picker.component';
import { CarResultsComponent } from '../../shared/components/car-results.component';
import { CarService } from '../../core/services/car.service';
import { CarWindow } from '../../core/models/car.model';
import { DURATION_PRESETS, PICKUP_TIMES, durationLabel, istDateValue, timeLabel } from '../../shared/utils/car-format';
import { scrollRowBy } from '../../shared/utils/scroll-row';

type LoadState = 'loading' | 'ready' | 'error';

interface MonthOption {
  value: string; // "yyyy-MM"
  label: string; // "October" — the next 12 months, so a month name alone is unambiguous
  short: string; // "Oct"
  year: number; // 2026
  full: string; // "October 2026" — shown in the hero's Travel Month field
}

/** This month and the following ones, for the hero month picker. */
function buildMonthOptions(count: number): MonthOption[] {
  const now = new Date();
  return Array.from({ length: count }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() + i, 1);
    const label = d.toLocaleDateString('en-IN', { month: 'long' });
    return {
      value: `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`,
      label,
      short: d.toLocaleDateString('en-IN', { month: 'short' }).replace('Sept', 'Sep'),
      year: d.getFullYear(),
      full: `${label} ${d.getFullYear()}`,
    };
  });
}

type SearchTab = 'trips' | 'cars' | 'holidays';

/** The hero's custom dropdowns (Cars / Holidays tabs); only one is open at a time. */
type HeroDropdown = 'carPickup' | 'carTime' | 'carDuration' | 'holidayMonth' | 'holidayTravellers';

/** "2026-10-03" in the visitor's local time — the earliest date the car date picker offers. */
function localDateValue(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
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
  imports: [CommonModule, FormsModule, TripCardComponent, DestinationCardComponent, FeatureCardComponent, ImageUrlPipe, DatePickerComponent, CarResultsComponent],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit, OnDestroy {
  private readonly tripService = inject(PublicTripService);
  private readonly destinationService = inject(PublicDestinationService);
  private readonly contactService = inject(ContactService);
  private readonly carService = inject(CarService);
  private readonly heroPhotoService = inject(HeroService);
  private readonly searchLog = inject(SearchLogService);
  private readonly siteFeatures = inject(FeatureService);
  /** appsettings Features:HideCarsAndHolidays — the search card shows Trips only (no tab bar). */
  readonly tripsOnly = this.siteFeatures.hideCarsAndHolidays;

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
    scrollRowBy(el, direction * Math.max(el.clientWidth * 0.85, 140), '.dcard', !this.reducedMotion);
  }

  // --- Upcoming Adventures: cards come from the server a page at a time ("View more" loads the
  // next page) — 8 per page on larger screens (two rows of four), 5 on phones. ---
  /** The location dropdown in the Upcoming Adventures header. */
  readonly locOpen = signal(false);
  /** Trips matching the current month / place / location, across all pages (from the server). */
  readonly tripsTotal = signal(0);
  private tripsPage = 1;
  readonly loadingMoreTrips = signal(false);

  private get tripsPageSize(): number {
    return typeof window !== 'undefined' && window.matchMedia('(max-width: 640px)').matches ? 5 : 8;
  }

  // Cards per row on larger screens (4 on laptops, fewer on tablets); rows are balanced and centred:
  // 5 cards → 3 + 2, 8 → 4 + 4, 1 → one card in the middle.
  private readonly viewportWidth = signal(typeof window !== 'undefined' ? window.innerWidth : 1440);
  readonly tripsMaxPerRow = computed(() => {
    const w = this.viewportWidth();
    return w >= 1100 ? 4 : w >= 860 ? 3 : 2;
  });
  readonly tripsPerRow = computed(() => {
    const n = this.upcomingTrips().length;
    const max = this.tripsMaxPerRow();
    if (n === 0) return max;
    return Math.ceil(n / Math.ceil(n / max));
  });

  @HostListener('window:resize')
  onWindowResize(): void {
    this.updateTrendArrows();
    this.viewportWidth.set(window.innerWidth);
  }

  // --- Hero search card: Trips / Cars / Holidays tabs (Trips by default) ---
  readonly searchTabs: { key: SearchTab; title: string; subtitle: string }[] = [
    { key: 'trips', title: 'Trips', subtitle: 'Join amazing group trips' },
    { key: 'cars', title: 'Cars', subtitle: 'Book cars & travellers' },
    { key: 'holidays', title: 'Holidays', subtitle: 'Custom holiday packages' },
  ];
  readonly searchTab = signal<SearchTab>('trips');

  selectSearchTab(tab: SearchTab): void {
    this.closeSearchPopovers();
    this.searchTab.set(tab);
  }

  // --- Phones: once the visitor starts using the search fields, the card scrolls up to the top of
  // the screen (navbar, "Meet. Travel. Explore." and the subtitle go off-screen) so fields, dropdowns and the
  // keyboard have the whole screen. If the page is too short to scroll that far (e.g. the Cars
  // tab before a search), a temporary spacer under the card makes room; it goes away on search or
  // on a tap outside the card.
  private readonly heroCard = viewChild<ElementRef<HTMLElement>>('heroCard');
  readonly searchRoom = signal(0);

  onSearchFieldsEngaged(event: Event): void {
    if (typeof window === 'undefined' || !window.matchMedia('(max-width: 640px)').matches) return;
    // The Search button scrolls to the results itself — don't pull the card back up over it.
    if ((event.target as Element | null)?.closest('.hx-go')) return;
    const card = this.heroCard()?.nativeElement;
    if (!card) return;
    const targetY = Math.round(window.scrollY + card.getBoundingClientRect().top - 8);
    if (Math.abs(window.scrollY - targetY) < 12) return;
    const maxScroll = document.documentElement.scrollHeight - this.searchRoom() - window.innerHeight;
    this.searchRoom.set(Math.max(0, targetY - maxScroll));
    setTimeout(() => window.scrollTo({ top: targetY, behavior: this.reducedMotion ? 'auto' : 'smooth' }));
  }

  // Custom dropdowns for the Cars / Holidays fields — same look as the Trips month picker, and the
  // whole box (or its label) opens them.
  readonly openDropdown = signal<HeroDropdown | null>(null);

  toggleDropdown(key: HeroDropdown): void {
    this.monthOpen.set(false);
    this.placeOpen.set(false);
    this.openDropdown.update((open) => (open === key ? null : key));
  }

  /** Quick picks under the Trips search — each runs the normal trip search for that place. Hidden for now. */
  readonly showPopularSearches = false;
  readonly popularSearches = ['Koraput', 'Araku', 'Mahendragiri', 'Puri', 'Balasore', 'Vizag'];

  searchPopular(place: string): void {
    this.searchPlace.set(place);
    this.applyFilters();
  }

  // Cars: pickup locations come from the approved cars (API); the search shows live results below the
  // card. Holidays have no booking system yet, so their search goes to the organizer on WhatsApp.
  readonly carPickups = signal<string[]>([]);
  readonly carPickup = signal('');
  readonly carDate = signal('');
  readonly carTimes = PICKUP_TIMES;
  readonly carTime = signal('10:00');
  readonly carTimeLabel = computed(() => timeLabel(this.carTime()));
  readonly carDurationPresets = DURATION_PRESETS;
  readonly carHours = signal(12);
  /** "Custom" duration: typed number of hours or days. */
  readonly carCustom = signal(false);
  readonly carCustomValue = signal(5);
  readonly carCustomUnit = signal<'hours' | 'days'>('days');
  readonly carDurationLabel = computed(() => durationLabel(this.carHours()));

  pickCarDuration(hours: number): void {
    this.carCustom.set(false);
    this.carHours.set(hours);
    this.openDropdown.set(null);
  }

  applyCustomDuration(): void {
    const value = Math.floor(this.carCustomValue() || 0);
    if (value < 1) return;
    this.carHours.set(this.carCustomUnit() === 'days' ? value * 24 : value);
    this.openDropdown.set(null);
  }
  readonly todayIso = localDateValue(new Date());

  readonly travellerOptions = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '10+'];
  readonly holidayPlace = signal('');
  readonly holidayMonth = signal('');
  readonly holidayMonthLabel = computed(() => this.monthOptions.find((m) => m.value === this.holidayMonth())?.full ?? 'Any month');
  readonly holidayTravellers = signal('2');

  /** The search the "Available vehicles" section below the hero is showing (null = not searched yet). */
  readonly carSearch = signal<{ city: string | null; window: CarWindow } | null>(null);

  searchCars(): void {
    this.closeSearchPopovers();
    this.searchRoom.set(0);
    // No date picked yet: tomorrow (pickups need a couple of hours' notice).
    const date = this.carDate() || istDateValue(1);
    this.carSearch.set({ city: this.carPickup() || null, window: { date, time: this.carTime(), hours: this.carHours() } });
    // Wait for the section to render, then bring it into view.
    setTimeout(() => this.scrollTo('car-results'));
  }

  exploreHolidays(): void {
    const month = this.monthOptions.find((m) => m.value === this.holidayMonth())?.full ?? 'Flexible';
    const travellers = this.holidayTravellers();
    const message = [
      "Hi Ghumo Odisha! I'd like a custom holiday package.",
      `Destination: ${this.holidayPlace().trim() || 'Open to suggestions'}`,
      `Travel month: ${month}`,
      `Travellers: ${travellers}`,
    ].join('\n');
    window.open(this.contactService.buildWhatsAppLink(message), '_blank');
  }

  // --- Trips tab: month picker + place text box with suggestions ---
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
  readonly placeQuery = signal('');

  // Every place seen while no month filter was applied, so the suggestions don't shrink
  // once a month search narrows the trip list.
  private readonly knownLocations = signal<string[]>([]);
  readonly placeOptions = computed(() => {
    const names = new Set(this.knownLocations());
    for (const d of this.destinations()) names.add(d.name);
    return Array.from(names).sort((a, b) => a.localeCompare(b));
  });

  // Type-ahead: nothing until the visitor types, then only the places matching what they've typed —
  // names starting with it first ("k" → Koraput before Puri-Konark), then names containing it.
  readonly placeSuggestions = computed(() => {
    const query = this.searchPlace().trim().toLowerCase();
    if (!query) return [];
    const matches = this.placeOptions().filter((p) => p.toLowerCase().includes(query));
    const starts = matches.filter((p) => p.toLowerCase().startsWith(query));
    return [...starts, ...matches.filter((p) => !p.toLowerCase().startsWith(query))];
  });

  toggleMonth(): void {
    this.placeOpen.set(false);
    this.openDropdown.set(null);
    this.monthOpen.update((open) => !open);
  }

  pickMonth(value: string): void {
    this.searchMonth.set(this.searchMonth() === value ? '' : value);
    this.monthOpen.set(false);
  }

  /** A click anywhere in the Destination box puts the cursor in it; suggestions appear once they type. */
  openPlace(): void {
    this.monthOpen.set(false);
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
    if (!target?.closest('.hx-dd')) this.openDropdown.set(null);
    if (!target?.closest('.hx-card')) this.searchRoom.set(0);
    if (!target?.closest('.up-loc')) this.locOpen.set(false);
  }

  @HostListener('document:keydown.escape')
  closeSearchPopovers(): void {
    this.monthOpen.set(false);
    this.placeOpen.set(false);
    this.openDropdown.set(null);
    this.locOpen.set(false);
  }

  readonly selectedLocation = signal<string | null>(null);

  // Every place with an upcoming trip (in the searched month, if any), from the server — the cards
  // arrive a page at a time, so they can't tell us every place.
  private readonly locationPool = signal<string[]>([]);

  // The Upcoming Adventures location dropdown: places that have trips, plus the selected place so
  // it stays visible.
  readonly distinctLocations = computed(() => {
    const names = new Set(this.locationPool());
    const selected = this.selectedLocation();
    if (selected) names.add(selected);
    return Array.from(names).sort((a, b) => a.localeCompare(b));
  });

  /** The server already filters to upcoming trips for the month / place / location. */
  readonly upcomingTrips = computed(() => this.trips());

  selectLocation(location: string | null, reload = true): void {
    this.selectedLocation.set(location);
    this.placeQuery.set('');
    // Keep the hero place box in step with the dropdown.
    this.searchPlace.set(location ?? '');
    this.locOpen.set(false);
    if (reload) this.loadTrips();
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
    this.siteFeatures.load().subscribe((f) => {
      if (f.hideCarsAndHolidays) {
        this.searchTab.set('trips');
        return;
      }
      this.carService.search({}).subscribe({ next: (r) => this.carPickups.set(r.locations), error: () => undefined });
    });
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

  private tripFilters() {
    const month = this.appliedMonth();
    return {
      ...(month ? monthRange(month) : {}),
      destination: this.selectedLocation() ?? undefined,
      search: this.placeQuery().trim() || undefined,
      upcomingOnly: true,
    };
  }

  // The month the location list was fetched for; it's fetched again only when the month changes.
  private locationsMonth: string | null = null;
  private locationsSeq = 0;

  private loadLocations(): void {
    const month = this.appliedMonth();
    if (this.locationsMonth === month) return;
    this.locationsMonth = month;
    const seq = ++this.locationsSeq;
    this.tripService.getTripLocations(month ? monthRange(month) : undefined).subscribe({
      next: (names) => {
        if (seq !== this.locationsSeq) return;
        this.locationPool.set(names);
        // Hero place suggestions: every place seen with no month applied.
        if (!month) this.knownLocations.set(Array.from(new Set([...this.knownLocations(), ...names])));
      },
      // Try again on the next load rather than keeping a missing list.
      error: () => {
        if (seq === this.locationsSeq) this.locationsMonth = null;
      },
    });
  }

  /** Loads the first page of Upcoming Adventures for the current month / place / location. */
  loadTrips(onLoaded?: () => void): void {
    const seq = ++this.tripLoadSeq;
    this.state.set('loading');
    this.loadingMoreTrips.set(false);
    this.loadLocations();
    this.tripService.getTrips(1, this.tripsPageSize, this.tripFilters()).subscribe({
      next: (r) => {
        if (seq !== this.tripLoadSeq) return;
        this.tripsPage = 1;
        this.trips.set(r.items);
        this.tripsTotal.set(r.totalCount);
        this.state.set('ready');
        onLoaded?.();
      },
      error: () => {
        if (seq === this.tripLoadSeq) this.state.set('error');
      },
    });
  }

  /** "View more": fetches the next page from the server and adds it below the current cards. */
  loadMoreTrips(): void {
    if (this.loadingMoreTrips() || this.trips().length >= this.tripsTotal()) return;
    const seq = this.tripLoadSeq;
    const nextPage = this.tripsPage + 1;
    this.loadingMoreTrips.set(true);
    this.tripService.getTrips(nextPage, this.tripsPageSize, this.tripFilters()).subscribe({
      next: (r) => {
        if (seq !== this.tripLoadSeq) return;
        this.tripsPage = nextPage;
        const seen = new Set(this.trips().map((t) => t.tripId));
        this.trips.update((list) => [...list, ...r.items.filter((t) => !seen.has(t.tripId))]);
        this.tripsTotal.set(r.totalCount);
        this.loadingMoreTrips.set(false);
      },
      error: () => {
        // The "View more" button comes back so the visitor can simply try again.
        if (seq === this.tripLoadSeq) this.loadingMoreTrips.set(false);
      },
    });
  }

  /** Hero "Search": applies month + place to Upcoming Trips and records the search for admins. */
  applyFilters(): void {
    this.closeSearchPopovers();
    this.searchRoom.set(0);
    // Close the phone keyboard so it doesn't cover the results the page scrolls to.
    (document.activeElement as HTMLElement | null)?.blur?.();
    const month = this.searchMonth();
    const text = this.searchPlace().trim();
    // An exact name, or text that clearly means one place ("pur" → Puri), selects that location in
    // Upcoming Adventures' dropdown; anything else is searched as free text.
    const lower = text.toLowerCase();
    const partials = lower ? this.placeOptions().filter((p) => p.toLowerCase().includes(lower)) : [];
    const exact = this.placeOptions().find((p) => p.toLowerCase() === lower) ?? (partials.length === 1 ? partials[0] : null);

    this.selectLocation(exact, false);
    if (!exact && text) {
      this.placeQuery.set(text);
      this.searchPlace.set(text);
    }
    this.appliedMonth.set(month);
    this.loadTrips(() => {
      if (month || text) {
        this.searchLog.record({ month: month || null, place: exact ?? (text || null), resultCount: this.tripsTotal() });
      }
    });
    this.jumpToUpcomingTrips();
  }

  readonly hasActiveFilters = computed(() => !!this.appliedMonth() || !!this.placeQuery());

  /** Empty-list message after a search, naming what isn't available (place and/or month). */
  readonly unavailableMessage = computed(() => {
    const month = this.monthOptions.find((m) => m.value === this.appliedMonth())?.full ?? '';
    const place = this.selectedLocation() ?? this.placeQuery();
    if (place && month) return `Trips to ${place} aren't available in ${month} yet. Try another month or explore other destinations.`;
    if (place) return `Trips to ${place} aren't available right now. Explore other destinations or check back soon.`;
    if (month) return `No trips are available in ${month} yet. Try another month.`;
    return 'No trips match your search.';
  });

  /**
   * Subtitle under "Upcoming Adventures": says what the list is showing after a search (in place of
   * filter chips); empty — and hidden — when nothing is searched.
   */
  readonly upcomingSubtitle = computed(() => {
    const month = this.monthOptions.find((m) => m.value === this.appliedMonth())?.full ?? '';
    const place = this.selectedLocation() ?? (this.placeQuery() ? `“${this.placeQuery()}”` : '');
    if (month && place) return `Trips to ${place} happening in ${month}.`;
    if (month) return `Trips happening in ${month}.`;
    if (this.placeQuery()) return `Trips matching ${place}.`;
    return '';
  });

  clearMonthFilter(): void {
    this.appliedMonth.set('');
    this.searchMonth.set('');
    this.loadTrips();
  }

  clearAllFilters(): void {
    this.selectLocation(null, false);
    this.clearMonthFilter();
  }

  scrollTo(id: string): void {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  openWhatsApp(): void {
    const message = "Hi! I'd like to know more about your upcoming Odisha trips.";
    window.open(this.contactService.buildWhatsAppLink(message), '_blank');
  }
}
