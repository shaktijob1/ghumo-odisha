import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminBookingService } from '../../../core/services/admin-booking.service';
import { AdminBookingListItem } from '../../../core/models/booking.model';
import { BookingSourceLabels, BookingStatus, PaymentStatus } from '../../../core/models/enums.model';
import { PagedResult } from '../../../core/models/api-response.model';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { StatusBadgeComponent } from '../../../shared/components/status-badge.component';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-booking-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent, StatusBadgeComponent],
  templateUrl: './booking-list.component.html',
})
export class BookingListComponent implements OnInit {
  private readonly bookingService = inject(AdminBookingService);

  readonly state = signal<LoadState>('loading');
  readonly result = signal<PagedResult<AdminBookingListItem> | null>(null);
  readonly search = signal('');
  readonly bookingStatus = signal<BookingStatus | null>(null);
  readonly paymentStatus = signal<PaymentStatus | null>(null);
  readonly page = signal(1);
  readonly pageSize = 10;

  readonly BookingStatus = BookingStatus;
  readonly PaymentStatus = PaymentStatus;
  readonly BookingSourceLabels = BookingSourceLabels;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.bookingService
      .getBookings(this.page(), this.pageSize, {
        bookingStatus: this.bookingStatus(),
        paymentStatus: this.paymentStatus(),
        search: this.search() || undefined,
      })
      .subscribe({
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

  onBookingStatusChange(value: string): void {
    this.bookingStatus.set(value === '' ? null : (Number(value) as BookingStatus));
    this.page.set(1);
    this.load();
  }

  onPaymentStatusChange(value: string): void {
    this.paymentStatus.set(value === '' ? null : (Number(value) as PaymentStatus));
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
}
