import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminCustomerService } from '../../../core/services/admin-customer.service';
import { AdminBookingService } from '../../../core/services/admin-booking.service';
import { AdminCustomerDetail } from '../../../core/models/customer.model';
import { BookingResponse } from '../../../core/models/booking.model';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, StatePanelComponent, StatusBadgeComponent],
  templateUrl: './customer-detail.component.html',
})
export class CustomerDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly customerService = inject(AdminCustomerService);
  private readonly bookingService = inject(AdminBookingService);

  readonly state = signal<LoadState>('loading');
  readonly customer = signal<AdminCustomerDetail | null>(null);
  readonly bookings = signal<BookingResponse[]>([]);

  customerId!: number;

  ngOnInit(): void {
    this.customerId = Number(this.route.snapshot.paramMap.get('id'));
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.customerService.getCustomer(this.customerId).subscribe({
      next: (c) => {
        this.customer.set(c);
        this.bookingService.getCustomerBookings(this.customerId, 1, 50).subscribe({
          next: (r) => {
            this.bookings.set(r.items);
            this.state.set('ready');
          },
          error: () => this.state.set('error'),
        });
      },
      error: () => this.state.set('error'),
    });
  }
}
