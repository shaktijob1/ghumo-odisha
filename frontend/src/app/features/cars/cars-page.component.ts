import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs';
import { CarService } from '../../core/services/car.service';
import { CarResultsComponent } from '../../shared/components/car-results.component';
import { VehicleSearchFormComponent } from '../../shared/components/vehicle-search-form.component';
import { VehicleSearch, searchFromParams, searchToParams } from '../../shared/utils/vehicle-search';

/**
 * /cars — search vehicles by date, pickup, drop, time and duration (the same form as the
 * home page's Vehicles tab). The search lives in the URL, so the results and the booking page share it
 * and "Cancel" on the booking page comes back to exactly these results.
 */
@Component({
  selector: 'app-cars-page',
  standalone: true,
  imports: [CommonModule, VehicleSearchFormComponent, CarResultsComponent],
  template: `
    <section class="cz-hero">
      <div class="container">
        <h1><span class="up-g">Book </span><span class="up-o">Vehicles</span></h1>
        <p class="sub">Cars and Tempo Travellers with a driver@if (bookingAmount(); as amount) { — pay just ₹{{ amount }} to confirm, the rest after your trip}.</p>
        <div class="hx-card cz-vcard">
          <div class="hx-panel">
            <app-vehicle-search-form [initial]="search()" (search)="onSearch($event)"></app-vehicle-search-form>
          </div>
        </div>
      </div>
    </section>
    @if (search(); as s) {
      <app-car-results [search]="s"></app-car-results>
    }
  `,
})
export class CarsPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly carService = inject(CarService);

  readonly search = toSignal(this.route.queryParamMap.pipe(map((p) => searchFromParams(p))), { requireSync: true });
  readonly bookingAmount = signal<number | null>(null);

  ngOnInit(): void {
    this.carService.settings().subscribe({ next: (s) => this.bookingAmount.set(s.bookingAmount), error: () => undefined });
  }

  onSearch(search: VehicleSearch): void {
    this.router.navigate(['/cars'], { queryParams: searchToParams(search) });
  }
}
