import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminCarFilter, CarAdminDashboard } from '../../../core/models/admin-car.model';
import { CarBookingStatus, CarBookingStatusLabels, carBookingBadgeClass } from '../../../core/models/car.model';
import { AdminCarService } from '../../../core/services/admin-car.service';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { istDateTime } from '../../../shared/utils/car-format';

type LoadState = 'loading' | 'ready' | 'error';

/** The Cars block on the admin dashboard: approval queues, fleet, trips, money and recent car bookings. */
@Component({
  selector: 'app-cars-dashboard-section',
  standalone: true,
  imports: [DecimalPipe, RouterLink, StatePanelComponent],
  template: `
    <div class="ahead" style="margin-top:26px">
      <div>
        <h2>Cars</h2>
        <div class="sub">Car rentals with owner-drivers</div>
      </div>
      <a class="btn sm ghost" routerLink="/admin/car-bookings">All car bookings</a>
    </div>

    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading" message="Loading Cars figures…"></app-state-panel> }
      @case ('error') { <app-state-panel kind="error" message="Could not load the Cars figures."></app-state-panel> }
      @case ('ready') {
        @let d = data()!;
        @if (d.pendingApprovals > 0 || d.refundsPending > 0) {
          <div class="acar-todo">
            @if (d.driversAwaitingReview) {
              <a [routerLink]="['/admin/drivers']" [queryParams]="{ tab: Review }"><b>{{ d.driversAwaitingReview }}</b> driver(s) to review</a>
            }
            @if (d.carsAwaitingReview) {
              <a [routerLink]="['/admin/cars']" [queryParams]="{ tab: Review }"><b>{{ d.carsAwaitingReview }}</b> car(s) to review</a>
            }
            @if (d.pricingAwaitingReview) {
              <a routerLink="/admin/cars/pricing"><b>{{ d.pricingAwaitingReview }}</b> pricing change(s) to approve</a>
            }
            @if (d.refundsPending) {
              <a [routerLink]="['/admin/car-bookings']" [queryParams]="{ tab: 'refund' }"><b>{{ d.refundsPending }}</b> refund(s) to issue</a>
            }
          </div>
        }

        <div class="kpis">
          <div class="kpi"><div class="k">Live cars</div><div class="v">{{ d.activeCars }}</div><div class="note">{{ d.approvedCars }} approved of {{ d.totalCars }}</div></div>
          <div class="kpi"><div class="k">Drivers</div><div class="v">{{ d.approvedDrivers }}</div><div class="note">approved of {{ d.totalDrivers }}</div></div>
          <div class="kpi"><div class="k">Upcoming car trips</div><div class="v">{{ d.upcomingBookings }}</div><div class="note">{{ d.tripsInProgress }} running now</div></div>
          <div class="kpi"><div class="k">Completed car trips</div><div class="v">{{ d.completedTrips }}</div></div>
        </div>
        <div class="kpis">
          <div class="kpi"><div class="k">Booking amounts collected</div><div class="v">₹{{ d.bookingAmountCollected | number: '1.0-0' }}</div><div class="note">online, not refunded</div></div>
          <div class="kpi"><div class="k">Completed trips' fares</div><div class="v">₹{{ d.completedTripsFareTotal | number: '1.0-0' }}</div><div class="note">mostly paid to drivers</div></div>
          <div class="kpi"><div class="k">Approvals waiting</div><div class="v">{{ d.pendingApprovals }}</div></div>
          <div class="kpi"><div class="k">Refunds to issue</div><div class="v">{{ d.refundsPending }}</div></div>
        </div>

        <div class="panel">
          <h4>Recent car bookings</h4>
          @if (d.recentBookings.length === 0) {
            <p class="note" style="margin-top:8px">No car bookings yet.</p>
          } @else {
            <div class="tblwrap" style="margin-top:10px">
              <table class="tbl">
                <tr><th>Booking</th><th>Pickup</th><th>Customer</th><th>Car</th><th>Driver</th><th class="num">Fare</th><th>Status</th></tr>
                @for (b of d.recentBookings; track b.carBookingId) {
                  <tr>
                    <td><a [routerLink]="['/admin/car-bookings', b.carBookingId]">{{ b.reference }}</a></td>
                    <td>{{ when(b.pickupAt) }}</td>
                    <td>{{ b.customerName }}</td>
                    <td>{{ b.carDisplayName }}</td>
                    <td>{{ b.driverName }}</td>
                    <td class="num">₹{{ (b.finalTotal ?? b.estimatedTotal) | number: '1.0-0' }}</td>
                    <td><span [class]="'badge ' + badge(b.status)">{{ labels[b.status] }}</span></td>
                  </tr>
                }
              </table>
            </div>
          }
        </div>
      }
    }
  `,
})
export class CarsDashboardSectionComponent implements OnInit {
  private readonly cars = inject(AdminCarService);

  readonly Review = AdminCarFilter.Review;
  readonly labels = CarBookingStatusLabels;
  readonly when = istDateTime;
  readonly state = signal<LoadState>('loading');
  readonly data = signal<CarAdminDashboard | null>(null);

  ngOnInit(): void {
    this.cars.dashboard().subscribe({
      next: (d) => {
        this.data.set(d);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  badge(status: CarBookingStatus): string {
    return carBookingBadgeClass(status);
  }
}
