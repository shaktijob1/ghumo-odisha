import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PublicTripService } from '../../core/services/public-trip.service';
import { TripSummary } from '../../core/models/trip.model';
import { TripCardComponent } from '../../shared/components/trip-card.component';
import { StatePanelComponent } from '../../shared/components/state-panel.component';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, FormsModule, TripCardComponent, StatePanelComponent],
  templateUrl: './home.component.html',
})
export class HomeComponent implements OnInit {
  private readonly tripService = inject(PublicTripService);

  readonly state = signal<LoadState>('loading');
  readonly trips = signal<TripSummary[]>([]);
  readonly search = signal('');

  readonly filteredTrips = computed(() => {
    const term = this.search().trim().toLowerCase();
    if (!term) return this.trips();
    return this.trips().filter((t) => t.title.toLowerCase().includes(term));
  });

  ngOnInit(): void {
    this.tripService.getTrips(1, 50).subscribe({
      next: (r) => {
        this.trips.set(r.items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
