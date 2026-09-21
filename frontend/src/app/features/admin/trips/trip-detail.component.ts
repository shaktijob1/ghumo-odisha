import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminTripService } from '../../../core/services/admin-trip.service';
import { AdminBookingService } from '../../../core/services/admin-booking.service';
import { AdminTripDetail } from '../../../core/models/trip.model';
import { AdminBookingListItem } from '../../../core/models/booking.model';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-trip-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, StatePanelComponent, StatusBadgeComponent],
  templateUrl: './trip-detail.component.html',
})
export class TripDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly tripService = inject(AdminTripService);
  private readonly bookingService = inject(AdminBookingService);

  readonly state = signal<LoadState>('loading');
  readonly trip = signal<AdminTripDetail | null>(null);
  readonly bookings = signal<AdminBookingListItem[]>([]);

  tripId!: number;

  ngOnInit(): void {
    this.tripId = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.tripService.getTrip(this.tripId).subscribe({
      next: (t) => {
        this.trip.set(t);
        this.bookingService.getTripBookings(this.tripId).subscribe({
          next: (b) => {
            this.bookings.set(b);
            this.state.set('ready');
          },
          error: () => this.state.set('error'),
        });
      },
      error: () => this.state.set('error'),
    });
  }

  bookingsForSlot(slotId: number): AdminBookingListItem[] {
    return this.bookings().filter((b) => b.tripDateSlotId === slotId);
  }

  totalSeats(t: AdminTripDetail): number {
    return t.dateSlots.reduce((sum, s) => sum + s.totalSeats, 0);
  }

  totalAvailable(t: AdminTripDetail): number {
    return t.dateSlots.reduce((sum, s) => sum + s.availableSeats, 0);
  }
}
