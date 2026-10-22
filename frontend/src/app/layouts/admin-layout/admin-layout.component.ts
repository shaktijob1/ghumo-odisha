import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminCarService } from '../../core/services/admin-car.service';
import { AdminRefundService } from '../../core/services/admin-refund.service';
import { ToastHostComponent } from '../../shared/components/toast-host.component';
import { AdminNavIcon, AdminNavIconComponent } from './admin-nav-icon.component';

interface NavItem {
  route: string;
  label: string;
  icon: AdminNavIcon;
  /** Icon tile colour. */
  color: string;
  exact?: boolean;
}

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastHostComponent, AdminNavIconComponent],
  templateUrl: './admin-layout.component.html',
})
export class AdminLayoutComponent implements OnInit {
  readonly auth = inject(AdminAuthService);
  readonly refunds = inject(AdminRefundService);
  readonly cars = inject(AdminCarService);
  private readonly router = inject(Router);

  /** Phones / tablets: the slide-in menu. */
  readonly menuOpen = signal(false);

  readonly mainNav: NavItem[] = [
    { route: '/admin/dashboard', label: 'Dashboard', icon: 'dashboard', color: '#0F6F5C' },
    { route: '/admin/trips', label: 'Trips', icon: 'trips', color: '#2E7D32' },
    { route: '/admin/destinations', label: 'Destinations', icon: 'destinations', color: '#D9541E' },
    { route: '/admin/holidays', label: 'Holidays', icon: 'holidays', color: '#EE6A2A' },
    { route: '/admin/stories', label: 'Stories', icon: 'stories', color: '#8E44AD' },
    { route: '/admin/bookings', label: 'Bookings', icon: 'bookings', color: '#1565C0' },
    { route: '/admin/refunds', label: 'Refunds', icon: 'refunds', color: '#C0483A' },
    { route: '/admin/collections', label: 'Collections', icon: 'collections', color: '#A77A00' },
    { route: '/admin/customers', label: 'Customers', icon: 'customers', color: '#0E7C86' },
    { route: '/admin/coupons', label: 'Coupons', icon: 'coupons', color: '#D63384' },
    { route: '/admin/logs', label: 'Logs', icon: 'logs', color: '#5C6B73' },
  ];

  readonly crmNav: NavItem[] = [
    { route: '/admin/crm', label: 'CRM dashboard', icon: 'dashboard', color: '#3A5B7A', exact: true },
    { route: '/admin/crm/customers', label: 'CRM customers', icon: 'crm', color: '#3A5B7A' },
    { route: '/admin/crm/follow-ups', label: 'Follow-ups', icon: 'followups', color: '#3A5B7A' },
    { route: '/admin/crm/departures', label: 'Trips / Campaigns', icon: 'campaigns', color: '#3A5B7A' },
    { route: '/admin/crm/pipeline', label: 'Pipeline', icon: 'pipeline', color: '#3A5B7A' },
    { route: '/admin/crm/whatsapp-templates', label: 'WhatsApp templates', icon: 'whatsapp', color: '#3A5B7A' },
    { route: '/admin/crm/communications', label: 'Communication history', icon: 'history', color: '#3A5B7A' },
    { route: '/admin/crm/reports', label: 'Reports', icon: 'reports', color: '#3A5B7A' },
  ];

  readonly carsNav: NavItem[] = [
    { route: '/admin/car-bookings', label: 'Car bookings', icon: 'bookings', color: '#9A6A11' },
    { route: '/admin/cars', label: 'Cars', icon: 'car', color: '#9A6A11' },
    { route: '/admin/drivers', label: 'Drivers', icon: 'driver', color: '#9A6A11' },
    { route: '/admin/service-areas', label: 'Pickup areas', icon: 'pin', color: '#9A6A11' },
  ];

  ngOnInit(): void {
    // Keep the "Refunds" badge current: refresh the pending count on every admin page change
    // (a cancellation elsewhere in the admin adds one). The phone menu closes on every page change.
    this.refreshRefundCount();
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe(() => {
      this.menuOpen.set(false);
      this.refreshRefundCount();
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.menuOpen.set(false);
  }

  signOut(): void {
    this.auth.logout();
  }

  private refreshRefundCount(): void {
    this.refunds.getCounts().subscribe({ error: () => undefined });
    // Cars badges (approvals waiting, car refunds to issue) come from the Cars dashboard figures.
    this.cars.dashboard().subscribe({ error: () => undefined });
  }
}
