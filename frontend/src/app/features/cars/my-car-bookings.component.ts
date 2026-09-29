import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CarBooking, CarBookingStatusLabels, carBookingBadgeClass } from '../../core/models/car.model';
import { CarService } from '../../core/services/car.service';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { durationLabel, istDateTime } from '../../shared/utils/car-format';

/** Car bookings block on /my-bookings. Renders nothing when the customer has none. */
@Component({
  selector: 'app-my-car-bookings',
  standalone: true,
  imports: [CommonModule, RouterLink, ImageUrlPipe],
  template: `
    @if (error()) {
      <p class="note" style="margin-bottom:16px">Could not load your car bookings. <button type="button" class="linkbtn" (click)="load()">Try again</button></p>
    }
    @if (bookings().length > 0) {
      <h3 class="cz-h3" style="margin-bottom:10px">Car bookings</h3>
      @for (b of bookings(); track b.carBookingId) {
        <a class="bkcard cz-bkrow" [routerLink]="['/my-bookings/cars', b.carBookingId]">
          <div class="shot" style="background:var(--canvas)">
            @if (b.carPhotoUrl) { <img [src]="b.carPhotoUrl | imageUrl" [alt]="b.carDisplayName" /> }
          </div>
          <div>
            <b>{{ b.carDisplayName }}</b>
            <div class="mut" style="font-size:12.5px">{{ b.reference }} · {{ when(b.pickupAt) }} · {{ duration(b.durationHours) }}</div>
            <div class="mut" style="font-size:12.5px">
              @if (b.final) { Final fare ₹{{ b.final.total | number: '1.0-0' }} } @else { Estimated ₹{{ b.estimate.total | number: '1.0-0' }} }
            </div>
          </div>
          <span class="badge" [class]="badge(b)">{{ label(b) }}</span>
        </a>
      }
      <div class="hr" style="margin:20px 0"></div>
    }
  `,
})
export class MyCarBookingsComponent implements OnInit {
  private readonly carService = inject(CarService);

  readonly bookings = signal<CarBooking[]>([]);
  readonly error = signal(false);
  readonly when = istDateTime;
  readonly duration = durationLabel;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.error.set(false);
    this.carService.myBookings().subscribe({ next: (b) => this.bookings.set(b), error: () => this.error.set(true) });
  }

  label(b: CarBooking): string {
    return CarBookingStatusLabels[b.status];
  }

  badge(b: CarBooking): string {
    return carBookingBadgeClass(b.status);
  }
}
