import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminBookingService } from '../../../core/services/admin-booking.service';
import { AdminTripService } from '../../../core/services/admin-trip.service';
import { AdminCustomerService } from '../../../core/services/admin-customer.service';
import { AdminTripListItem, DateSlot } from '../../../core/models/trip.model';
import { AdminCustomerListItem } from '../../../core/models/customer.model';
import { BookingSource, BookingStatus } from '../../../core/models/enums.model';
import { ApiResponse } from '../../../core/models/api-response.model';
import { ToastService } from '../../../core/services/toast.service';

@Component({
  selector: 'app-booking-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './booking-form.component.html',
})
export class BookingFormComponent implements OnInit {
  private readonly bookingService = inject(AdminBookingService);
  private readonly tripService = inject(AdminTripService);
  private readonly customerService = inject(AdminCustomerService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly BookingSource = BookingSource;

  readonly trips = signal<AdminTripListItem[]>([]);
  readonly slots = signal<DateSlot[]>([]);
  readonly customerResults = signal<AdminCustomerListItem[]>([]);
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  isNewCustomer = false;
  customerSearch = '';
  selectedCustomer: AdminCustomerListItem | null = null;

  newCustomerName = '';
  newCustomerPhoneNumber = '';
  newCustomerEmail = '';

  selectedTripId: number | null = null;
  selectedSlotId: number | null = null;
  numberOfSeats = 1;
  advanceAmount = 0;
  bookingSource: BookingSource = BookingSource.Phone;
  initialStatus: BookingStatus = BookingStatus.Requested;
  adminNotes = '';

  ngOnInit(): void {
    this.tripService.getTrips(1, 100).subscribe((r) => this.trips.set(r.items));
  }

  onCustomerSearchChange(): void {
    if (!this.customerSearch) {
      this.customerResults.set([]);
      return;
    }

    this.customerService.getCustomers(1, 10, this.customerSearch).subscribe((r) => this.customerResults.set(r.items));
  }

  pickCustomer(customer: AdminCustomerListItem): void {
    this.selectedCustomer = customer;
    this.customerResults.set([]);
    this.customerSearch = `${customer.name} — ${customer.phoneNumber}`;
  }

  onTripChange(): void {
    this.selectedSlotId = null;
    this.slots.set([]);
    if (!this.selectedTripId) return;

    this.tripService.getTrip(this.selectedTripId).subscribe((t) => this.slots.set(t.dateSlots));
  }

  get selectedTripPrice(): number {
    return this.trips().find((t) => t.tripId === this.selectedTripId)?.amountPerPerson ?? 0;
  }

  get totalAmount(): number {
    return this.selectedTripPrice * this.numberOfSeats;
  }

  get remaining(): number {
    return Math.max(0, this.totalAmount - this.advanceAmount);
  }

  submit(): void {
    this.errorMessage.set(null);

    if (!this.selectedTripId || !this.selectedSlotId || this.numberOfSeats <= 0) {
      this.errorMessage.set('Choose a trip, a date and the number of seats.');
      return;
    }

    if (!this.isNewCustomer && !this.selectedCustomer) {
      this.errorMessage.set('Search for and select an existing customer, or switch to "New customer".');
      return;
    }

    if (this.isNewCustomer && (!this.newCustomerName || !this.newCustomerPhoneNumber)) {
      this.errorMessage.set('Enter the new customer\'s name and phone number.');
      return;
    }

    this.submitting.set(true);

    this.bookingService
      .createManualBooking({
        customerId: this.isNewCustomer ? null : this.selectedCustomer!.customerId,
        newCustomerName: this.isNewCustomer ? this.newCustomerName : null,
        newCustomerPhoneNumber: this.isNewCustomer ? this.newCustomerPhoneNumber : null,
        newCustomerEmail: this.isNewCustomer ? this.newCustomerEmail || null : null,
        tripId: this.selectedTripId,
        tripDateSlotId: this.selectedSlotId,
        numberOfSeats: this.numberOfSeats,
        advanceAmount: this.advanceAmount,
        bookingSource: this.bookingSource,
        initialStatus: this.initialStatus,
        adminNotes: this.adminNotes || null,
      })
      .subscribe({
        next: (res) => {
          this.submitting.set(false);
          this.toast.success('Booking created.');
          this.router.navigate(['/admin/bookings', res.bookingId]);
        },
        error: (err: HttpErrorResponse) => {
          this.submitting.set(false);
          const body = err.error as ApiResponse<unknown> | undefined;
          this.errorMessage.set(body?.message ?? 'Could not create the booking.');
        },
      });
  }
}
