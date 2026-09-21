import { Component, Input } from '@angular/core';
import {
  BookingStatus,
  BookingStatusLabels,
  PaymentStatus,
  PaymentStatusLabels,
  bookingStatusBadgeClass,
} from '../../core/models/enums.model';
import { TripStatus } from '../../core/models/enums.model';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  template: `<span class="badge" [class]="cssClass">{{ label }}</span>`,
})
export class StatusBadgeComponent {
  @Input({ required: true }) kind!: 'booking' | 'payment' | 'trip';
  @Input({ required: true }) value!: number;

  get label(): string {
    switch (this.kind) {
      case 'booking':
        return BookingStatusLabels[this.value as BookingStatus];
      case 'payment':
        return PaymentStatusLabels[this.value as PaymentStatus];
      case 'trip':
        return this.value === TripStatus.Active ? 'Active' : 'Inactive';
    }
  }

  get cssClass(): string {
    if (this.kind === 'booking') {
      return bookingStatusBadgeClass(this.value as BookingStatus);
    }

    if (this.kind === 'payment') {
      return this.value === PaymentStatus.Paid || this.value === PaymentStatus.AdvancePaid ? 'ok' : this.value === PaymentStatus.Refunded ? 'bad' : '';
    }

    return this.value === TripStatus.Active ? 'ok' : 'bad';
  }
}
