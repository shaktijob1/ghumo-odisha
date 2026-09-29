import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs';
import { CarWindow } from '../../core/models/car.model';
import { CarService } from '../../core/services/car.service';
import { CarResultsComponent } from '../../shared/components/car-results.component';
import { CarWindowFormComponent } from '../../shared/components/car-window-form.component';
import { defaultWindow, windowFromParams, windowToParams } from '../../shared/utils/car-format';

/**
 * /cars — search cars and Tempo Travellers by pickup location, date, time and duration. The search
 * lives in the URL (?city=&date=&time=&hours=), so results, details and the booking page all keep it.
 */
@Component({
  selector: 'app-cars-page',
  standalone: true,
  imports: [CommonModule, CarWindowFormComponent, CarResultsComponent],
  template: `
    <section class="cz-hero">
      <div class="container">
        <h1><span class="up-g">Book </span><span class="up-o">Cars</span></h1>
        <p class="sub">Cars and Tempo Travellers with a driver@if (bookingAmount(); as amount) { — pay just ₹{{ amount }} to confirm, the rest after your trip}.</p>
        <div class="cz-searchcard">
          <app-car-window-form [initial]="window()" [initialCity]="city()" [locations]="locations()" (search)="onSearch($event)"></app-car-window-form>
        </div>
      </div>
    </section>
    <app-car-results [city]="city()" [window]="window()"></app-car-results>
  `,
})
export class CarsPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly carService = inject(CarService);

  readonly window = toSignal(this.route.queryParamMap.pipe(map((p) => windowFromParams(p) ?? defaultWindow())), { requireSync: true });
  readonly city = toSignal(this.route.queryParamMap.pipe(map((p) => p.get('city'))), { requireSync: true });

  readonly locations = signal<string[]>([]);
  readonly bookingAmount = signal<number | null>(null);

  ngOnInit(): void {
    this.carService.search({}).subscribe({ next: (r) => this.locations.set(r.locations), error: () => undefined });
    this.carService.settings().subscribe({ next: (s) => this.bookingAmount.set(s.bookingAmount), error: () => undefined });
  }

  onSearch(e: { window: CarWindow; city: string | null }): void {
    this.router.navigate(['/cars'], { queryParams: windowToParams(e.window, { city: e.city }) });
  }
}
