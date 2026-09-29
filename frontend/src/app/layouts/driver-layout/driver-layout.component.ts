import { NgTemplateOutlet } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { DriverAuthService } from '../../core/services/driver-auth.service';
import { ToastHostComponent } from '../../shared/components/toast-host.component';

/**
 * Driver area shell. Built for a phone first: a slim top bar and a bottom tab bar within thumb
 * reach; on larger screens the tabs sit in the top bar instead.
 */
@Component({
  selector: 'app-driver-layout',
  standalone: true,
  imports: [NgTemplateOutlet, RouterOutlet, RouterLink, RouterLinkActive, ToastHostComponent],
  template: `
    <header class="dv-top">
      <div class="dv-wrap dv-topin">
        <a routerLink="/driver" class="dv-logo">Ghumo <em>Odisha</em> <span>Driver</span></a>
        <nav class="dv-nav dv-nav-top" aria-label="Driver">
          <ng-container *ngTemplateOutlet="links"></ng-container>
        </nav>
        <div class="dv-me">
          <span class="mut">{{ firstName() }}</span>
          <button type="button" class="btn ghost sm" (click)="auth.logout()">Sign out</button>
        </div>
      </div>
    </header>
    <main class="dv-wrap dv-main">
      <router-outlet></router-outlet>
    </main>
    <nav class="dv-nav dv-nav-bottom" aria-label="Driver">
      <ng-container *ngTemplateOutlet="links"></ng-container>
    </nav>

    <ng-template #links>
      <a routerLink="/driver" routerLinkActive="on" [routerLinkActiveOptions]="{ exact: true }">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1z"></path></svg>
        <span>Home</span>
      </a>
      <a routerLink="/driver/bookings" routerLinkActive="on">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="9"></circle><circle cx="12" cy="12" r="2.5"></circle><path d="M12 14.5V21M9.6 11.3 3.5 9.5M14.4 11.3l6.1-1.8"></path></svg>
        <span>Trips</span>
      </a>
      <a routerLink="/driver/cars" routerLinkActive="on">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 17H3.5a1 1 0 0 1-1-1v-3.2a2 2 0 0 1 .6-1.4L5 9.5l1.7-3.4A2 2 0 0 1 8.5 5h7a2 2 0 0 1 1.8 1.1L19 9.5l1.9 1.9a2 2 0 0 1 .6 1.4V16a1 1 0 0 1-1 1H19"></path><circle cx="7.5" cy="17" r="2"></circle><circle cx="16.5" cy="17" r="2"></circle><path d="M9.5 17h5"></path></svg>
        <span>Cars</span>
      </a>
      <a routerLink="/driver/earnings" routerLinkActive="on">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M6 3h12M6 8h12M14 21 6 13h3a5 5 0 0 0 0-10"></path></svg>
        <span>Earnings</span>
      </a>
      <a routerLink="/driver/profile" routerLinkActive="on">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="8" r="4"></circle><path d="M4 21a8 8 0 0 1 16 0"></path></svg>
        <span>Profile</span>
      </a>
    </ng-template>
    <app-toast-host></app-toast-host>
  `,
})
export class DriverLayoutComponent {
  readonly auth = inject(DriverAuthService);

  firstName(): string {
    return (this.auth.currentDriver()?.name ?? '').split(' ')[0] || 'Driver';
  }
}
