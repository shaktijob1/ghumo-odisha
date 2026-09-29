import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApprovalLabels, CarStatus, DriverStatus, approvalBadgeClass } from '../../core/models/car.model';
import { DriverBooking, DriverBookingScope, DriverCar, DriverProfile } from '../../core/models/driver.model';
import { DriverAuthService } from '../../core/services/driver-auth.service';
import { DriverService } from '../../core/services/driver.service';
import { ToastService } from '../../core/services/toast.service';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { apiErrorMessage } from '../../shared/utils/api-error';
import { durationLabel, istDateTime } from '../../shared/utils/car-format';

/** /driver — account status and what to do next, the running trip, upcoming trips and cars at a glance. */
@Component({
  selector: 'app-driver-home',
  standalone: true,
  imports: [CommonModule, RouterLink, ImageUrlPipe, StatePanelComponent],
  template: `
    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') {
        <div class="card pad cz-empty"><p>{{ error() }}</p><button type="button" class="btn ghost sm" (click)="load()">Try again</button></div>
      }
      @default {
        @if (profile(); as p) {
          <h1 class="dv-h1">Hi, {{ firstName() }}</h1>

          @for (t of active(); track t.booking.carBookingId) {
            <a class="card pad dv-live" [routerLink]="['/driver/bookings', t.booking.carBookingId]">
              <span class="dv-pulse" aria-hidden="true"></span>
              <div><b>Trip in progress</b><span>{{ t.customerName }} · {{ t.booking.carDisplayName }}</span></div>
              <span class="btn sm">Open</span>
            </a>
          }

          <section class="card pad dv-status">
            <div class="cz-cardhead">
              <h3 class="cz-h3">Your account</h3>
              <span class="badge" [class]="driverBadge()">{{ driverLabel() }}</span>
            </div>
            @switch (p.status) {
              @case (Driver.Approved) {
                <p class="mut">You're approved. Approved cars with approved pricing show up in customer search.</p>
              }
              @case (Driver.Suspended) {
                <p><b>Your account is suspended.</b> {{ p.statusReason }}</p>
                <p class="note">Please contact Ghumo Odisha.</p>
              }
              @case (Driver.Rejected) {
                <p><b>Your profile wasn't approved.</b> {{ p.statusReason }}</p>
                <a class="btn sm" routerLink="/driver/profile" style="margin-top:10px">Fix and send again</a>
              }
              @default {
                @if (p.submittedForReviewAt) {
                  <p class="mut">Your profile is with our team for review. We'll approve it soon — you can keep adding your car meanwhile.</p>
                } @else if (p.missingForReview.length) {
                  <p class="mut" style="margin-bottom:8px">Finish these to send your profile for approval:</p>
                  <ul class="dv-todo">
                    @for (m of p.missingForReview; track m) { <li>{{ m }}</li> }
                  </ul>
                  <a class="btn sm" routerLink="/driver/profile" style="margin-top:10px">Complete profile</a>
                } @else {
                  <p class="mut">Your profile is complete.</p>
                  @if (submitError()) { <div class="errorbox" style="margin-top:10px">{{ submitError() }}</div> }
                  <button type="button" class="btn sm" style="margin-top:10px" [disabled]="submitting()" (click)="submit()">
                    @if (submitting()) { <span class="spin"></span> } @else { Send for approval }
                  </button>
                }
              }
            }
          </section>

          <section class="card pad">
            <div class="cz-cardhead">
              <h3 class="cz-h3">Upcoming trips</h3>
              <a class="linkbtn" routerLink="/driver/bookings">All trips</a>
            </div>
            @if (upcoming().length === 0) {
              <p class="mut">No upcoming trips yet. Bookings appear here once a customer pays for one of your approved cars.</p>
            } @else {
              @for (t of upcoming().slice(0, 3); track t.booking.carBookingId) {
                <a class="dv-row" [routerLink]="['/driver/bookings', t.booking.carBookingId]">
                  <div>
                    <b>{{ when(t.booking.pickupAt) }}</b>
                    <span class="mut">{{ t.customerName }} · {{ duration(t.booking.durationHours) }} · {{ t.booking.pickupCity }}</span>
                  </div>
                  <span class="dv-chev" aria-hidden="true">›</span>
                </a>
              }
            }
          </section>

          <section class="card pad">
            <div class="cz-cardhead">
              <h3 class="cz-h3">Your cars</h3>
              <a class="btn ghost sm" routerLink="/driver/cars/new">+ Add car</a>
            </div>
            @if (cars().length === 0) {
              <p class="mut">Add your car with photos and pricing to start getting bookings.</p>
            } @else {
              @for (c of cars(); track c.carId) {
                <a class="dv-row" [routerLink]="['/driver/cars', c.carId]">
                  @if (cover(c); as img) { <img class="dv-thumb" [src]="img | imageUrl" alt="" /> } @else { <span class="dv-thumb"></span> }
                  <div>
                    <b>{{ c.displayName }}</b>
                    <span class="mut">{{ c.registrationNumber }} · {{ c.category }}</span>
                  </div>
                  @if (c.isListed) {
                    <span class="badge ok">Live</span>
                  } @else {
                    <span class="badge" [class]="carBadge(c)">{{ carLabel(c) }}</span>
                  }
                </a>
              }
            }
          </section>
        }
      }
    }
  `,
})
export class DriverHomeComponent implements OnInit {
  private readonly driverService = inject(DriverService);
  private readonly auth = inject(DriverAuthService);
  private readonly toast = inject(ToastService);

  readonly Driver = DriverStatus;
  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly error = signal('');
  readonly profile = signal<DriverProfile | null>(null);
  readonly cars = signal<DriverCar[]>([]);
  readonly upcoming = signal<DriverBooking[]>([]);
  readonly active = signal<DriverBooking[]>([]);
  readonly submitting = signal(false);
  readonly submitError = signal<string | null>(null);

  readonly firstName = computed(() => (this.profile()?.name || 'there').split(' ')[0]);
  readonly driverLabel = computed(() => {
    const p = this.profile();
    if (!p) return '';
    if (p.status === DriverStatus.Pending && !p.submittedForReviewAt) return 'Profile incomplete';
    return ApprovalLabels.driver[p.status];
  });
  readonly driverBadge = computed(() => approvalBadgeClass(this.profile()?.status ?? 0, DriverStatus.Approved, [DriverStatus.Rejected, DriverStatus.Suspended]));

  readonly when = istDateTime;
  readonly duration = durationLabel;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    forkJoin({
      profile: this.driverService.profile(),
      cars: this.driverService.cars(),
      upcoming: this.driverService.bookings(DriverBookingScope.Upcoming),
      active: this.driverService.bookings(DriverBookingScope.Active),
    }).subscribe({
      next: (r) => {
        this.profile.set(r.profile);
        this.cars.set(r.cars);
        this.upcoming.set(r.upcoming);
        this.active.set(r.active);
        this.auth.updateCached({ name: r.profile.name, status: r.profile.status });
        this.state.set('ready');
      },
      error: (e: unknown) => {
        this.error.set(apiErrorMessage(e, 'Could not load your account.'));
        this.state.set('error');
      },
    });
  }

  submit(): void {
    this.submitting.set(true);
    this.submitError.set(null);
    this.driverService.submitProfile().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.submitting.set(false);
        this.toast.success('Sent for approval. We’ll review it soon.');
      },
      error: (e: unknown) => {
        this.submitting.set(false);
        this.submitError.set(apiErrorMessage(e));
      },
    });
  }

  cover(c: DriverCar): string | null {
    return c.photos.find((p) => p.kind === 0)?.imageUrl ?? c.photos[0]?.imageUrl ?? null;
  }

  carLabel(c: DriverCar): string {
    if (c.status === CarStatus.Pending && !c.submittedForReviewAt) return 'Not submitted';
    return ApprovalLabels.car[c.status];
  }

  carBadge(c: DriverCar): string {
    return approvalBadgeClass(c.status, CarStatus.Approved, [CarStatus.Rejected, CarStatus.Suspended, CarStatus.Inactive]);
  }
}
