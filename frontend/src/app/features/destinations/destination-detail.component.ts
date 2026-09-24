import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PublicDestinationService } from '../../core/services/public-destination.service';
import { DestinationDetail } from '../../core/models/destination.model';
import { TripSummary } from '../../core/models/trip.model';
import { TripCardComponent } from '../../shared/components/trip-card.component';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { DatePickerComponent } from '../../shared/components/date-picker.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';

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
    return [
      { label: 'Best season', value: d.bestSeason },
      { label: 'From Bhubaneswar', value: d.distanceFromBhubaneswar },
      { label: 'Ideal duration', value: d.idealDuration },
      { label: 'Known for', value: d.knownFor },
    ].filter((f): f is { label: string; value: string } => !!f.value);
  });

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
