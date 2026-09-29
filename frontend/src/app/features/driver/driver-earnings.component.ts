import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CarPaymentStatus } from '../../core/models/car.model';
import { DriverEarnings } from '../../core/models/driver.model';
import { DriverService } from '../../core/services/driver.service';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { apiErrorMessage } from '../../shared/utils/api-error';
import { istDate } from '../../shared/utils/car-format';

/** /driver/earnings — completed trips, km, fares and balance collected vs still to collect. */
@Component({
  selector: 'app-driver-earnings',
  standalone: true,
  imports: [CommonModule, RouterLink, StatePanelComponent],
  template: `
    <h1 class="dv-h1">Earnings</h1>
    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <div class="card pad cz-empty"><p>{{ error() }}</p><button type="button" class="btn ghost sm" (click)="load()">Try again</button></div> }
      @default {
        @if (data(); as e) {
          <div class="dv-stats">
            <div class="card pad"><span>Total fares</span><b>₹{{ e.totalFare | number: '1.0-0' }}</b><small>{{ e.completedTrips }} trip{{ e.completedTrips === 1 ? '' : 's' }} · {{ e.totalKm | number }} km</small></div>
            <div class="card pad"><span>Collected by you</span><b>₹{{ e.balanceCollected | number: '1.0-0' }}</b><small>balances after trips</small></div>
            <div class="card pad"><span>Still to collect</span><b [class.dv-due]="e.balancePending > 0">₹{{ e.balancePending | number: '1.0-0' }}</b><small>completed, not marked collected</small></div>
            <div class="card pad"><span>Paid online to Ghumo Odisha</span><b>₹{{ e.bookingAmountsPaidOnline | number: '1.0-0' }}</b><small>booking amounts</small></div>
          </div>
          <section class="card pad">
            <h3 class="cz-h3">Recent trips</h3>
            @if (e.recentTrips.length === 0) {
              <p class="mut">Completed trips show up here. {{ e.upcomingTrips ? e.upcomingTrips + ' upcoming.' : '' }}</p>
            } @else {
              @for (t of e.recentTrips; track t.carBookingId) {
                <a class="dv-row" [routerLink]="['/driver/bookings', t.carBookingId]">
                  <div><b>{{ date(t.pickupAt) }} · {{ t.customerName }}</b><span class="mut">{{ t.reference }} · {{ t.carDisplayName }}</span></div>
                  <div style="text-align:right"><b>₹{{ (t.finalTotal ?? 0) | number: '1.0-0' }}</b>
                    <span class="mut" style="display:block;font-size:11.5px">{{ t.paymentStatus === Paid ? 'Collected' : 'To collect' }}</span></div>
                </a>
              }
            }
          </section>
        }
      }
    }
  `,
})
export class DriverEarningsComponent implements OnInit {
  private readonly driverService = inject(DriverService);

  readonly Paid = CarPaymentStatus.BalanceCollected;
  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly error = signal('');
  readonly data = signal<DriverEarnings | null>(null);
  readonly date = istDate;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.driverService.earnings().subscribe({
      next: (e) => {
        this.data.set(e);
        this.state.set('ready');
      },
      error: (e: unknown) => {
        this.error.set(apiErrorMessage(e, 'Could not load your earnings.'));
        this.state.set('error');
      },
    });
  }
}
