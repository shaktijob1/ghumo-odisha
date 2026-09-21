import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminTripService } from '../../../core/services/admin-trip.service';
import { AdminTripListItem } from '../../../core/models/trip.model';
import { TripStatus } from '../../../core/models/enums.model';
import { PagedResult } from '../../../core/models/api-response.model';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog.component';
import { ToastService } from '../../../core/services/toast.service';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-trip-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent, StatusBadgeComponent, ConfirmDialogComponent],
  templateUrl: './trip-list.component.html',
})
export class TripListComponent implements OnInit {
  private readonly tripService = inject(AdminTripService);
  private readonly toast = inject(ToastService);

  readonly state = signal<LoadState>('loading');
  readonly result = signal<PagedResult<AdminTripListItem> | null>(null);
  readonly search = signal('');
  readonly status = signal<TripStatus | null>(null);
  readonly page = signal(1);
  readonly pageSize = 10;

  readonly deactivateTarget = signal<AdminTripListItem | null>(null);

  readonly TripStatus = TripStatus;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.tripService.getTrips(this.page(), this.pageSize, this.search() || undefined, this.status()).subscribe({
      next: (r) => {
        this.result.set(r);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  onSearchChange(value: string): void {
    this.search.set(value);
    this.page.set(1);
    this.load();
  }

  onStatusChange(value: string): void {
    this.status.set(value === '' ? null : (Number(value) as TripStatus));
    this.page.set(1);
    this.load();
  }

  goToPage(p: number): void {
    this.page.set(p);
    this.load();
  }

  get totalPages(): number {
    const r = this.result();
    return r ? Math.max(1, Math.ceil(r.totalCount / this.pageSize)) : 1;
  }

  confirmDeactivate(): void {
    const trip = this.deactivateTarget();
    if (!trip) return;

    this.tripService.deleteTrip(trip.tripId).subscribe({
      next: () => {
        this.toast.success('Trip deactivated.');
        this.deactivateTarget.set(null);
        this.load();
      },
      error: () => this.deactivateTarget.set(null),
    });
  }
}
