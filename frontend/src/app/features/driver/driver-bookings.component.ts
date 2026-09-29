import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CarBookingStatusLabels, carBookingBadgeClass } from '../../core/models/car.model';
import { DriverBooking, DriverBookingScope } from '../../core/models/driver.model';
import { DriverService } from '../../core/services/driver.service';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { apiErrorMessage } from '../../shared/utils/api-error';
import { durationLabel, istDateTime } from '../../shared/utils/car-format';

/** /driver/bookings — the driver's trips: upcoming, in progress, history. */
@Component({
  selector: 'app-driver-bookings',
  standalone: true,
  imports: [CommonModule, RouterLink, StatePanelComponent],
  template: `
    <h1 class="dv-h1">Trips</h1>
    <div class="cz-tabs dv-tabs" role="tablist">
      @for (t of tabs; track t.scope) {
        <button type="button" role="tab" [class.on]="scope() === t.scope" [attr.aria-selected]="scope() === t.scope" (click)="select(t.scope)">{{ t.label }}</button>
      }
    </div>
    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <div class="card pad cz-empty"><p>{{ error() }}</p><button type="button" class="btn ghost sm" (click)="load()">Try again</button></div> }
      @default {
        @if (items().length === 0) {
          <app-state-panel kind="empty" [message]="emptyText()"></app-state-panel>
        } @else {
          @for (d of items(); track d.booking.carBookingId) {
            <a class="card pad dv-trip" [routerLink]="['/driver/bookings', d.booking.carBookingId]">
              <div>
                <b>{{ when(d.booking.pickupAt) }}</b>
                <span class="mut">{{ d.customerName }} · {{ duration(d.booking.durationHours) }} · {{ d.booking.carDisplayName }}</span>
                <span class="mut">
                  @if (d.booking.final) { Final ₹{{ d.booking.final.total | number: '1.0-0' }} · {{ d.booking.trip?.actualKm | number }} km }
                  @else { Est. ₹{{ d.booking.estimate.total | number: '1.0-0' }} · {{ d.booking.estimate.km | number }} km }
                </span>
              </div>
              <span class="badge" [class]="badge(d)">{{ label(d) }}</span>
            </a>
          }
        }
      }
    }
  `,
})
export class DriverBookingsComponent implements OnInit {
  private readonly driverService = inject(DriverService);

  readonly tabs = [
    { scope: DriverBookingScope.Upcoming, label: 'Upcoming' },
    { scope: DriverBookingScope.Active, label: 'In progress' },
    { scope: DriverBookingScope.History, label: 'Past' },
  ];
  readonly scope = signal(DriverBookingScope.Upcoming);
  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly error = signal('');
  readonly items = signal<DriverBooking[]>([]);
  readonly when = istDateTime;
  readonly duration = durationLabel;

  ngOnInit(): void {
    this.load();
  }

  select(scope: DriverBookingScope): void {
    this.scope.set(scope);
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.driverService.bookings(this.scope()).subscribe({
      next: (b) => {
        this.items.set(b);
        this.state.set('ready');
      },
      error: (e: unknown) => {
        this.error.set(apiErrorMessage(e, 'Could not load your trips.'));
        this.state.set('error');
      },
    });
  }

  emptyText(): string {
    switch (this.scope()) {
      case DriverBookingScope.Upcoming:
        return 'No upcoming trips.';
      case DriverBookingScope.Active:
        return 'No trip is running right now.';
      default:
        return 'No past trips yet.';
    }
  }

  label(d: DriverBooking): string {
    return CarBookingStatusLabels[d.booking.status];
  }

  badge(d: DriverBooking): string {
    return carBookingBadgeClass(d.booking.status);
  }
}
