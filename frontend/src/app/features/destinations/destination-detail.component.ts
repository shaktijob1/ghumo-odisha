import { CommonModule } from '@angular/common';
import { Component, ElementRef, HostListener, OnInit, computed, effect, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PublicDestinationService } from '../../core/services/public-destination.service';
import { SeoService } from '../../core/services/seo.service';
import { DestinationDetail } from '../../core/models/destination.model';
import { TripSummary } from '../../core/models/trip.model';
import { TripCardComponent } from '../../shared/components/trip-card.component';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { DatePickerComponent } from '../../shared/components/date-picker.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { scrollRowBy } from '../../shared/utils/scroll-row';

type LoadState = 'loading' | 'ready' | 'error' | 'not-found';
type TripsState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-destination-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, TripCardComponent, StatePanelComponent, DatePickerComponent, ImageUrlPipe],
  templateUrl: './destination-detail.component.html',
})
export class DestinationDetailComponent implements OnInit {
  readonly todayIso = new Date().toISOString().slice(0, 10);
  private readonly route = inject(ActivatedRoute);
  private readonly destinationService = inject(PublicDestinationService);
  private readonly seo = inject(SeoService);

  readonly state = signal<LoadState>('loading');
  readonly destination = signal<DestinationDetail | null>(null);

  readonly tripsState = signal<TripsState>('loading');
  readonly trips = signal<TripSummary[]>([]);
  readonly totalCount = signal(0);

  readonly fromDate = signal('');

  readonly hasActiveFilters = computed(() => !!this.fromDate());

  readonly factList = computed(() => {
    const d = this.destination();
    if (!d) return [];
    const facts: { label: string; value: string | null; icon: 'sun' | 'route' | 'clock' | 'star' }[] = [
      { label: 'Best season', value: d.bestSeason, icon: 'sun' },
      { label: 'From Bhubaneswar', value: d.distanceFromBhubaneswar, icon: 'route' },
      { label: 'Ideal duration', value: d.idealDuration, icon: 'clock' },
      { label: 'Known for', value: d.knownFor, icon: 'star' },
    ];
    return facts.filter((f): f is { label: string; value: string; icon: 'sun' | 'route' | 'clock' | 'star' } => !!f.value);
  });

  // --- Trips row: same scroll + prev/next arrows as the home page's Upcoming Trips ---
  private readonly tripScroll = viewChild<ElementRef<HTMLElement>>('tripScroll');
  readonly tripCanScrollPrev = signal(false);
  readonly tripCanScrollNext = signal(false);
  private readonly tripArrowSync = effect(() => {
    this.trips();
    const el = this.tripScroll()?.nativeElement;
    if (el) setTimeout(() => { el.scrollLeft = 0; this.updateTripArrows(); });
  });
  private readonly reducedMotion =
    typeof window !== 'undefined' && !!window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;

  updateTripArrows(): void {
    const el = this.tripScroll()?.nativeElement;
    if (!el) return;
    this.tripCanScrollPrev.set(el.scrollLeft > 2);
    this.tripCanScrollNext.set(el.scrollLeft + el.clientWidth < el.scrollWidth - 2);
  }

  /** Moves the row by 2 cards (1 on phones, where about one card fits). */
  scrollTrips(direction: 1 | -1): void {
    const el = this.tripScroll()?.nativeElement;
    const card = el?.querySelector<HTMLElement>('.tslot');
    if (!el || !card) return;
    const gap = parseFloat(getComputedStyle(el).columnGap) || 0;
    const step = card.getBoundingClientRect().width + gap;
    const cards = step * 2 <= el.clientWidth ? 2 : 1;
    scrollRowBy(el, direction * cards * step, '.tslot', !this.reducedMotion);
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    this.updateTripArrows();
  }

  private slug = '';

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('slug');
      if (!slug) return;
      this.slug = slug;
      this.fromDate.set('');
      this.loadDestination();
    });
  }

  loadDestination(): void {
    this.state.set('loading');
    this.destinationService.getDestination(this.slug).subscribe({
      next: (d) => {
        this.destination.set(d);
        // Same title/description the API writes into the first page load (SeoPageRenderer.DestinationPage).
        const about = (d.aboutText ?? d.tagline ?? d.knownFor ?? '').replace(/\s+/g, ' ').trim();
        const summary = `Explore ${d.name}, Odisha with Ghumo Odisha group trips and tour packages. ${about}`.trim();
        this.seo.setPage({
          title: `${d.name} Tour Packages & Trips | Ghumo Odisha`,
          description: summary.length > 160 ? summary.slice(0, summary.lastIndexOf(' ', 159)) + '…' : summary,
          path: `/destinations/${d.slug}`,
          image: d.heroImageUrl,
        });
        this.state.set('ready');
        this.loadTrips();
      },
      error: (err) => this.state.set(err?.status === 404 ? 'not-found' : 'error'),
    });
  }

  loadTrips(): void {
    this.tripsState.set('loading');
    this.destinationService
      .getDestinationTrips(this.slug, 1, 50, {
        fromDate: this.fromDate() || undefined,
      })
      .subscribe({
        next: (r) => {
          this.trips.set(r.items);
          this.totalCount.set(r.totalCount);
          this.tripsState.set('ready');
        },
        error: () => this.tripsState.set('error'),
      });
  }

  applyFilters(): void {
    this.loadTrips();
  }

  clearDateFilter(): void {
    this.fromDate.set('');
    this.loadTrips();
  }
}
