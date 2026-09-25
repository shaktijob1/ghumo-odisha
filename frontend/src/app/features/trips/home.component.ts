import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PublicTripService } from '../../core/services/public-trip.service';
import { HeroService } from '../../core/services/hero.service';
import { TripSummary } from '../../core/models/trip.model';
import { TripCardComponent } from '../../shared/components/trip-card.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';

type LoadState = 'loading' | 'ready' | 'error';

/**
 * Same hero + Upcoming Trips section as the dashboard (minus the Trending Destinations carousel,
 * which stays homepage-only) — this page is the full trip listing, so "View All" reveals every
 * upcoming trip instead of paginating in fixed pages like the homepage teaser does.
 */
@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, FormsModule, TripCardComponent, ImageUrlPipe],
  templateUrl: './home.component.html',
})
export class HomeComponent implements OnInit, OnDestroy {
  private readonly tripService = inject(PublicTripService);
  private readonly heroPhotoService = inject(HeroService);

  private readonly tripsHeroPhoto = this.heroPhotoService.get('trips');
  private readonly tripsHeroPhotoFailed = signal(false);
  private readonly siteHeroPhoto = this.heroPhotoService.get('home');
  private readonly siteHeroPhotoFailed = signal(false);

  readonly state = signal<LoadState>('loading');
  private readonly trips = signal<TripSummary[]>([]);

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

  private static readonly TRIPS_PAGE_SIZE = 6;
  readonly visibleTripsCount = signal(HomeComponent.TRIPS_PAGE_SIZE);
  readonly visibleUpcomingTrips = computed(() => this.upcomingTrips().slice(0, this.visibleTripsCount()));

  showMoreTrips(): void {
    this.visibleTripsCount.update((n) => n + HomeComponent.TRIPS_PAGE_SIZE);
  }

  viewAllTrips(): void {
    this.visibleTripsCount.set(this.upcomingTrips().length);
  }

  selectLocation(location: string | null): void {
    this.selectedLocation.set(location);
    this.visibleTripsCount.set(HomeComponent.TRIPS_PAGE_SIZE);
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
    this.locationSearch.set('');
    this.showLocationSuggestions.set(false);
    this.selectLocation(location);
    this.jumpToUpcomingTrips();
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

  // Admin-uploaded hero photos take priority over cycling trip cover photos: this page's own Trips
  // photo first, then the Home page photo. Each one that fails to load is skipped in turn.
  readonly heroImage = computed(() => {
    const trips = this.tripsHeroPhoto();
    if (trips && !this.tripsHeroPhotoFailed()) return trips;
    const home = this.siteHeroPhoto();
    if (home && !this.siteHeroPhotoFailed()) return home;
    return this.heroImageCandidates()[this.heroImageIndex()] ?? null;
  });

  onHeroImageError(): void {
    if (this.tripsHeroPhoto() && !this.tripsHeroPhotoFailed()) {
      this.tripsHeroPhotoFailed.set(true);
      return;
    }
    if (this.siteHeroPhoto() && !this.siteHeroPhotoFailed()) {
      this.siteHeroPhotoFailed.set(true);
      return;
    }
    this.heroImageIndex.update((i) => i + 1);
  }

  ngOnInit(): void {
    this.loadTrips();
  }

  ngOnDestroy(): void {
    if (this.highlightTimer) clearTimeout(this.highlightTimer);
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
          this.visibleTripsCount.set(HomeComponent.TRIPS_PAGE_SIZE);
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

  private hasUpcomingSlot(trip: TripSummary): boolean {
    if (!trip.nextSlotStartDate) return false;
    const start = new Date(trip.nextSlotStartDate);
    const todayStart = new Date();
    todayStart.setHours(0, 0, 0, 0);
    return start >= todayStart;
  }
}
