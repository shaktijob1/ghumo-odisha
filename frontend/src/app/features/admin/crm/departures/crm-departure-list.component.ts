import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { AdminTripService } from '../../../../core/services/admin-trip.service';
import { ToastService } from '../../../../core/services/toast.service';
import { AdminTripListItem } from '../../../../core/models/trip.model';
import { CrmDeparture, CrmDepartureStatusLabels, CrmDepartureStatusTone } from '../../../../core/models/crm.model';
import { StatePanelComponent } from '../../../../shared/components/state-panel.component';
import { CrmDepartureDialogComponent } from './crm-departure-dialog.component';

const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

function num(value: string | null): number | null {
  if (value === null || value === '') return null;
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

/** Admin → CRM → Trips / Campaigns: every departure with seats, demand and its badge. */
@Component({
  selector: 'app-crm-departure-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent, CrmDepartureDialogComponent],
  templateUrl: './crm-departure-list.component.html',
  styleUrls: ['../crm-shared.css', './crm-departures.css'],
})
export class CrmDepartureListComponent implements OnInit {
  private readonly crm = inject(AdminCrmService);
  private readonly tripService = inject(AdminTripService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly statusLabels = CrmDepartureStatusLabels;
  readonly statusTone = CrmDepartureStatusTone;
  readonly monthNames = MONTH_NAMES;
  readonly years = (() => {
    const now = new Date().getFullYear();
    return Array.from({ length: 6 }, (_, i) => now + 1 - i);
  })();

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly items = signal<CrmDeparture[]>([]);
  readonly total = signal(0);
  readonly trips = signal<AdminTripListItem[]>([]);
  readonly dialogOpen = signal(false);

  readonly pageSize = 30;
  page = 1;
  tripId: number | null = null;
  year: number | null = null;
  month: number | null = null;
  includePast = false;

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.total() / this.pageSize));
  }

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    this.tripId = num(q.get('tripId'));
    this.year = num(q.get('year'));
    this.month = this.year ? num(q.get('month')) : null;
    this.includePast = q.get('past') === '1';
    this.tripService.getTrips(1, 100).subscribe({ next: (r) => this.trips.set(r.items), error: () => undefined });
    this.load();
  }

  apply(): void {
    this.page = 1;
    this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: { tripId: this.tripId, year: this.year, month: this.year ? this.month : null, past: this.includePast ? 1 : null },
    });
    this.load();
  }

  goToPage(page: number): void {
    this.page = Math.min(Math.max(1, page), this.totalPages);
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.crm
      .departures({ tripId: this.tripId, year: this.year, month: this.year ? this.month : null, includePast: this.includePast, page: this.page, pageSize: this.pageSize })
      .subscribe({
        next: (r) => {
          this.items.set(r.items);
          this.total.set(r.totalCount);
          this.state.set('ready');
        },
        error: () => this.state.set('error'),
      });
  }

  onCreated(id: number): void {
    this.dialogOpen.set(false);
    this.toast.success('Departure created.');
    this.router.navigate(['/admin/crm/departures', id]);
  }

  seatShare(d: CrmDeparture): number {
    return d.totalSeats ? Math.min(100, Math.round((d.bookedSeats * 100) / d.totalSeats)) : 0;
  }

  minimumMark(d: CrmDeparture): number | null {
    return d.minimumBookedSeats && d.totalSeats ? Math.round((d.minimumBookedSeats * 100) / d.totalSeats) : null;
  }
}
