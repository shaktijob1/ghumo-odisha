import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PagedResult } from '../../../core/models/api-response.model';
import {
  CarBookingStatus,
  CarBookingStatusLabels,
  CarBookingSummary,
  CarPaymentStatus,
  CarPaymentStatusLabels,
  carBookingBadgeClass,
} from '../../../core/models/car.model';
import { AdminCarService } from '../../../core/services/admin-car.service';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { istDateTime } from '../../../shared/utils/car-format';

type LoadState = 'loading' | 'ready' | 'error';

interface Tab {
  key: string;
  label: string;
  status: CarBookingStatus | null;
  paymentStatus: CarPaymentStatus | null;
  empty: string;
}

const TABS: Tab[] = [
  { key: 'all', label: 'All', status: null, paymentStatus: null, empty: 'No car bookings yet.' },
  { key: 'upcoming', label: 'Confirmed', status: CarBookingStatus.Confirmed, paymentStatus: null, empty: 'No confirmed bookings waiting to start.' },
  { key: 'running', label: 'In progress', status: CarBookingStatus.InProgress, paymentStatus: null, empty: 'No trips running right now.' },
  { key: 'completed', label: 'Completed', status: CarBookingStatus.Completed, paymentStatus: null, empty: 'No completed trips yet.' },
  { key: 'cancelled', label: 'Cancelled', status: CarBookingStatus.Cancelled, paymentStatus: null, empty: 'No cancelled bookings.' },
  { key: 'refund', label: 'Refunds to issue', status: null, paymentStatus: CarPaymentStatus.RefundPending, empty: 'No refunds waiting. Paid bookings that get cancelled appear here.' },
  { key: 'refunding', label: 'Refunds issued', status: null, paymentStatus: CarPaymentStatus.RefundProcessing, empty: 'No refunds waiting to be marked as received.' },
  { key: 'unpaid', label: 'Not paid', status: CarBookingStatus.Expired, paymentStatus: null, empty: 'No unpaid bookings.' },
];

@Component({
  selector: 'app-admin-car-booking-list',
  standalone: true,
  imports: [DecimalPipe, FormsModule, RouterLink, StatePanelComponent],
  template: `
    <div class="ahead">
      <div>
        <h2>Car bookings</h2>
        <div class="sub">Paid bookings, running trips and booking-amount refunds. Bookings never paid for are under "Not paid".</div>
      </div>
    </div>

    <div class="daytabs">
      @for (t of tabs; track t.key) {
        <button class="daytab" [class.on]="tab().key === t.key" (click)="selectTab(t)">
          {{ t.label }}
          @if (t.key === 'refund' && cars.refundsPending() > 0) { <span class="badge wait" style="margin-left:4px;padding:1px 7px">{{ cars.refundsPending() }}</span> }
        </button>
      }
    </div>

    <div class="filters">
      <input class="sel srch" placeholder="Search booking ID (GC-…), customer, phone, driver or number plate…" [ngModel]="search()" (ngModelChange)="onSearch($event)" />
    </div>

    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <app-state-panel kind="error" message="Could not load car bookings."></app-state-panel> }
      @case ('ready') {
        @let r = result()!;
        @if (r.items.length === 0) {
          <app-state-panel kind="empty" [message]="search() ? 'No bookings match your search.' : tab().empty"></app-state-panel>
        } @else {
          <div class="panel" style="padding:16px">
            <div class="tblwrap">
              <table class="tbl">
                <tr><th>Booking</th><th>Pickup</th><th>Customer</th><th>Car</th><th>Driver</th><th class="num">Fare</th><th>Status</th><th>Payment</th></tr>
                @for (b of r.items; track b.carBookingId) {
                  <tr>
                    <td><a [routerLink]="['/admin/car-bookings', b.carBookingId]"><b>{{ b.reference }}</b></a></td>
                    <td>{{ when(b.pickupAt) }}</td>
                    <td>{{ b.customerName }}</td>
                    <td>{{ b.carDisplayName }}</td>
                    <td>{{ b.driverName }}</td>
                    <td class="num">
                      @if (b.finalTotal !== null) {
                        <b>₹{{ b.finalTotal | number: '1.0-0' }}</b><div class="note">final</div>
                      } @else {
                        ₹{{ b.estimatedTotal | number: '1.0-0' }}<div class="note">estimate</div>
                      }
                    </td>
                    <td><span [class]="'badge ' + statusBadge(b.status)">{{ statusLabels[b.status] }}</span></td>
                    <td><span [class]="'badge ' + paymentBadge(b.paymentStatus)">{{ paymentLabels[b.paymentStatus] }}</span></td>
                  </tr>
                }
              </table>
            </div>
            <div class="pager">
              <span>{{ r.totalCount }} booking(s) · page {{ page() }} of {{ totalPages }}</span>
              <div class="pg">
                <button (click)="goToPage(page() - 1)" [disabled]="page() <= 1">‹</button>
                <button (click)="goToPage(page() + 1)" [disabled]="page() >= totalPages">›</button>
              </div>
            </div>
          </div>
        }
      }
    }
  `,
})
export class AdminCarBookingListComponent implements OnInit {
  readonly cars = inject(AdminCarService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly tabs = TABS;
  readonly statusLabels = CarBookingStatusLabels;
  readonly paymentLabels = CarPaymentStatusLabels;
  readonly when = istDateTime;

  readonly state = signal<LoadState>('loading');
  readonly result = signal<PagedResult<CarBookingSummary> | null>(null);
  readonly tab = signal<Tab>(TABS[0]);
  readonly search = signal('');
  readonly page = signal(1);
  readonly pageSize = 20;

  ngOnInit(): void {
    const key = this.route.snapshot.queryParamMap.get('tab');
    const found = TABS.find((t) => t.key === key);
    if (found) this.tab.set(found);
    this.load();
    this.cars.dashboard().subscribe({ error: () => undefined });
  }

  load(): void {
    this.state.set('loading');
    const t = this.tab();
    this.cars.bookings(t.status, t.paymentStatus, this.search(), this.page(), this.pageSize).subscribe({
      next: (r) => {
        this.result.set(r);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  selectTab(t: Tab): void {
    this.tab.set(t);
    this.page.set(1);
    this.router.navigate([], { queryParams: { tab: t.key }, replaceUrl: true });
    this.load();
  }

  onSearch(value: string): void {
    this.search.set(value);
    this.page.set(1);
    this.load();
  }

  goToPage(p: number): void {
    this.page.set(p);
    this.load();
  }

  statusBadge(status: CarBookingStatus): string {
    return carBookingBadgeClass(status);
  }

  paymentBadge(status: CarPaymentStatus): string {
    return carPaymentBadgeClass(status);
  }

  get totalPages(): number {
    const r = this.result();
    return r ? Math.max(1, Math.ceil(r.totalCount / this.pageSize)) : 1;
  }
}

export function carPaymentBadgeClass(status: CarPaymentStatus): string {
  switch (status) {
    case CarPaymentStatus.BookingAmountPaid:
    case CarPaymentStatus.BalanceCollected:
    case CarPaymentStatus.Refunded:
      return 'ok';
    case CarPaymentStatus.RefundPending:
      return 'wait';
    case CarPaymentStatus.RefundProcessing:
      return 'info';
    default:
      return 'bad';
  }
}
