import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApprovalLabels, CarStatus, approvalBadgeClass } from '../../core/models/car.model';
import { DriverCar } from '../../core/models/driver.model';
import { DriverService } from '../../core/services/driver.service';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { apiErrorMessage } from '../../shared/utils/api-error';

/** /driver/cars — the driver's cars with their approval status. */
@Component({
  selector: 'app-driver-cars',
  standalone: true,
  imports: [CommonModule, RouterLink, ImageUrlPipe, StatePanelComponent],
  template: `
    <div class="cz-cardhead" style="margin-bottom:14px">
      <h1 class="dv-h1" style="margin:0">Your cars</h1>
      <a class="btn sm" routerLink="/driver/cars/new">+ Add car</a>
    </div>
    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <div class="card pad cz-empty"><p>{{ error() }}</p><button type="button" class="btn ghost sm" (click)="load()">Try again</button></div> }
      @default {
        @if (cars().length === 0) {
          <div class="card pad cz-empty">
            <p>You haven't added a car yet.</p>
            <a class="btn" routerLink="/driver/cars/new">Add your car</a>
          </div>
        } @else {
          @for (c of cars(); track c.carId) {
            <a class="card pad dv-carrow" [routerLink]="['/driver/cars', c.carId]">
              @if (cover(c); as img) { <img [src]="img | imageUrl" alt="" /> } @else { <span class="dv-noimg"></span> }
              <div>
                <b>{{ c.displayName }}</b>
                <span class="mut">{{ c.registrationNumber }} · {{ c.category }}</span>
                @if (c.pendingPricing) { <span class="badge wait">New pricing pending</span> }
              </div>
              @if (c.isListed) {
                <span class="badge ok">Live</span>
              } @else {
                <span class="badge" [class]="badge(c)">{{ label(c) }}</span>
              }
            </a>
          }
        }
      }
    }
  `,
})
export class DriverCarsComponent implements OnInit {
  private readonly driverService = inject(DriverService);

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly error = signal('');
  readonly cars = signal<DriverCar[]>([]);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.driverService.cars().subscribe({
      next: (c) => {
        this.cars.set(c);
        this.state.set('ready');
      },
      error: (e: unknown) => {
        this.error.set(apiErrorMessage(e, 'Could not load your cars.'));
        this.state.set('error');
      },
    });
  }

  cover(c: DriverCar): string | null {
    return c.photos.find((p) => p.kind === 0)?.imageUrl ?? c.photos[0]?.imageUrl ?? null;
  }

  label(c: DriverCar): string {
    return c.status === CarStatus.Pending && !c.submittedForReviewAt ? 'Not submitted' : ApprovalLabels.car[c.status];
  }

  badge(c: DriverCar): string {
    return approvalBadgeClass(c.status, CarStatus.Approved, [CarStatus.Rejected, CarStatus.Suspended, CarStatus.Inactive]);
  }
}
