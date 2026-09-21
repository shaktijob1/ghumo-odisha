import { Routes } from '@angular/router';
import { adminGuard } from './core/guards/admin.guard';
import { customerGuard } from './core/guards/customer.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./layouts/customer-layout/customer-layout.component').then((m) => m.CustomerLayoutComponent),
    children: [
      { path: '', loadComponent: () => import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent) },
      { path: 'trips', loadComponent: () => import('./features/trips/home.component').then((m) => m.HomeComponent) },
      { path: 'trips/:id', loadComponent: () => import('./features/trips/trip-detail.component').then((m) => m.TripDetailComponent) },
      {
        path: 'cars',
        data: { title: 'Cars' },
        loadComponent: () => import('./features/misc/coming-soon.component').then((m) => m.ComingSoonComponent),
      },
      {
        path: 'hotels',
        data: { title: 'Hotels' },
        loadComponent: () => import('./features/misc/coming-soon.component').then((m) => m.ComingSoonComponent),
      },
      { path: 'login', loadComponent: () => import('./features/auth/customer-auth.component').then((m) => m.CustomerAuthComponent) },
      {
        path: 'my-bookings',
        canActivate: [customerGuard],
        loadComponent: () => import('./features/account/my-bookings.component').then((m) => m.MyBookingsComponent),
      },
      {
        path: 'profile',
        canActivate: [customerGuard],
        loadComponent: () => import('./features/account/profile.component').then((m) => m.ProfileComponent),
      },
    ],
  },
  {
    path: 'admin/login',
    loadComponent: () => import('./features/admin/auth/admin-login.component').then((m) => m.AdminLoginComponent),
  },
  {
    path: 'admin',
    loadComponent: () => import('./layouts/admin-layout/admin-layout.component').then((m) => m.AdminLayoutComponent),
    canActivate: [adminGuard],
    children: [
      {
        path: 'dashboard',
        loadComponent: () => import('./features/admin/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'trips',
        loadComponent: () => import('./features/admin/trips/trip-list.component').then((m) => m.TripListComponent),
      },
      {
        path: 'trips/add',
        loadComponent: () => import('./features/admin/trips/trip-form.component').then((m) => m.TripFormComponent),
      },
      {
        path: 'trips/:id/edit',
        loadComponent: () => import('./features/admin/trips/trip-form.component').then((m) => m.TripFormComponent),
      },
      {
        path: 'trips/:id',
        loadComponent: () => import('./features/admin/trips/trip-detail.component').then((m) => m.TripDetailComponent),
      },
      {
        path: 'bookings',
        loadComponent: () => import('./features/admin/bookings/booking-list.component').then((m) => m.BookingListComponent),
      },
      {
        path: 'bookings/add',
        loadComponent: () => import('./features/admin/bookings/booking-form.component').then((m) => m.BookingFormComponent),
      },
      {
        path: 'bookings/:id',
        loadComponent: () => import('./features/admin/bookings/booking-detail.component').then((m) => m.BookingDetailComponent),
      },
      {
        path: 'customers',
        loadComponent: () => import('./features/admin/customers/customer-list.component').then((m) => m.CustomerListComponent),
      },
      {
        path: 'coupons',
        loadComponent: () => import('./features/admin/coupons/coupon-list.component').then((m) => m.CouponListComponent),
      },
      {
        path: 'customers/:id',
        loadComponent: () => import('./features/admin/customers/customer-detail.component').then((m) => m.CustomerDetailComponent),
      },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  { path: '**', loadComponent: () => import('./features/misc/not-found.component').then((m) => m.NotFoundComponent) },
];
