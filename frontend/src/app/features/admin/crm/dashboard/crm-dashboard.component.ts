import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { AdminTripService } from '../../../../core/services/admin-trip.service';
import { AdminTripListItem } from '../../../../core/models/trip.model';
import {
  CrmDashboard,
  CrmDashboardFilter,
  CrmDeparture,
  CrmDepartureStatusLabels,
  CrmDepartureStatusTone,
  CrmStatus,
  CrmStatusLabels,
  CrmStatusTone,
  CrmStatuses,
  LeadSource,
  LeadSourceLabels,
  LeadSources,
} from '../../../../core/models/crm.model';
import { StatePanelComponent } from '../../../../shared/components/state-panel.component';

const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

/** Number from a query-string value, or null. */
function num(value: string | null): number | null {
  if (value === null || value === '') return null;
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

@Component({
  selector: 'app-crm-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent],
  templateUrl: './crm-dashboard.component.html',
  styleUrls: ['../crm-shared.css', './crm-dashboard.component.css'],
})
export class CrmDashboardComponent implements OnInit {
  private readonly crm = inject(AdminCrmService);
  private readonly tripService = inject(AdminTripService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly CrmStatus = CrmStatus;
  readonly statusLabels = CrmStatusLabels;
  readonly statusTone = CrmStatusTone;
  readonly statuses = CrmStatuses;
  readonly sourceLabels = LeadSourceLabels;
  readonly sources = LeadSources;
  readonly departureLabels = CrmDepartureStatusLabels;
  readonly departureTone = CrmDepartureStatusTone;
  readonly monthNames = MONTH_NAMES;

  // Years to pick from: a few back for history, one ahead for trips being sold now. Never a fixed list.
  readonly years = (() => {
    const now = new Date().getFullYear();
    return Array.from({ length: 6 }, (_, i) => now + 1 - i);
  })();

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly data = signal<CrmDashboard | null>(null);
  readonly trips = signal<AdminTripListItem[]>([]);
  readonly departures = signal<CrmDeparture[]>([]);

  // Filters (kept in the URL so a filtered view can be refreshed or shared).
  year: number | null = null;
  month: number | null = null;
  tripId: number | null = null;
  tripDateSlotId: number | null = null;
  source: LeadSource | null = null;
  status: CrmStatus | null = null;

  /** "November 2026", "2026", or "All time" — the period the numbers cover. */
  readonly periodLabel = signal('All time');

  /** The stage breakdown, in pipeline order, with each stage's share of all leads. */
  readonly stages = computed(() => {
    const d = this.data();
    if (!d) return [];
    const counts: [CrmStatus, number][] = [
      [CrmStatus.NewLead, d.newLeads],
      [CrmStatus.Interested, d.interested],
      [CrmStatus.HotLead, d.hotLeads],
      [CrmStatus.FollowUp, d.followUp],
      [CrmStatus.BookingPending, d.bookingPending],
      [CrmStatus.Booked, d.booked],
      [CrmStatus.Completed, d.completed],
      [CrmStatus.Cancelled, d.cancelled],
      [CrmStatus.NotInterested, d.notInterested],
      [CrmStatus.Later, d.later],
    ];
    return counts.map(([status, count]) => ({ status, count, share: d.totalLeads ? Math.round((count * 100) / d.totalLeads) : 0 }));
  });

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    this.year = num(q.get('year'));
    this.month = this.year ? num(q.get('month')) : null;
    this.tripId = num(q.get('tripId'));
    this.tripDateSlotId = this.tripId ? num(q.get('tripDateSlotId')) : null;
    this.source = num(q.get('source')) as LeadSource | null;
    this.status = num(q.get('status')) as CrmStatus | null;

    this.tripService.getTrips(1, 100).subscribe({ next: (r) => this.trips.set(r.items), error: () => undefined });
    if (this.tripId) this.loadDepartures();
    this.load();
  }

  onYearChange(): void {
    if (!this.year) this.month = null;
    this.apply();
  }

  onTripChange(): void {
    this.tripDateSlotId = null;
    this.departures.set([]);
    if (this.tripId) this.loadDepartures();
    this.apply();
  }

  /** Quick pick: this calendar month. */
  thisMonth(): void {
    const now = new Date();
    this.year = now.getFullYear();
    this.month = now.getMonth() + 1;
    this.apply();
  }

  clearFilters(): void {
    this.year = this.month = this.tripId = this.tripDateSlotId = null;
    this.source = this.status = null;
    this.departures.set([]);
    this.apply();
  }

  get hasFilters(): boolean {
    return [this.year, this.tripId, this.source, this.status].some((v) => v !== null);
  }

  apply(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        year: this.year,
        month: this.year ? this.month : null,
        tripId: this.tripId,
        tripDateSlotId: this.tripId ? this.tripDateSlotId : null,
        source: this.source,
        status: this.status,
      },
    });
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.periodLabel.set(this.year ? (this.month ? `${MONTH_NAMES[this.month - 1]} ${this.year}` : `${this.year}`) : 'All time');
    const filter: CrmDashboardFilter = {
      year: this.year,
      month: this.year ? this.month : null,
      tripId: this.tripId,
      tripDateSlotId: this.tripDateSlotId,
      source: this.source,
      status: this.status,
    };
    this.crm.dashboard(filter).subscribe({
      next: (d) => {
        this.data.set(d);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  private loadDepartures(): void {
    this.crm.departures({ tripId: this.tripId, includePast: true, pageSize: 100 }).subscribe({
      next: (r) => this.departures.set(r.items),
      error: () => this.departures.set([]),
    });
  }

  /** Booked share of a departure's seats, for the seat bar. */
  seatShare(d: CrmDeparture): number {
    return d.totalSeats ? Math.min(100, Math.round((d.bookedSeats * 100) / d.totalSeats)) : 0;
  }

  /** Where the minimum-bookings mark sits on the seat bar. */
  minimumMark(d: CrmDeparture): number | null {
    return d.minimumBookedSeats && d.totalSeats ? Math.round((d.minimumBookedSeats * 100) / d.totalSeats) : null;
  }
}
