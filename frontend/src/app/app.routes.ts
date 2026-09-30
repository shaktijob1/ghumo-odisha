import { Routes } from '@angular/router';
import { adminGuard } from './core/guards/admin.guard';
import { carsEnabledGuard } from './core/guards/cars-enabled.guard';
import { customerGuard } from './core/guards/customer.guard';
import { driverGuard } from './core/guards/driver.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./layouts/customer-layout/customer-layout.component').then((m) => m.CustomerLayoutComponent),
    children: [
      { path: '', loadComponent: () => import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent) },
      { path: 'trips', title: 'Odisha Tour Packages & Group Trips | Ghumo Odisha', loadComponent: () => import('./features/trips/home.component').then((m) => m.HomeComponent) },
      { path: 'trips/:id', loadComponent: () => import('./features/trips/trip-detail.component').then((m) => m.TripDetailComponent) },
      {
        path: 'destinations/:slug',
        loadComponent: () => import('./features/destinations/destination-detail.component').then((m) => m.DestinationDetailComponent),
      },
      // Customer Cars pages: redirect home while appsettings Features:HideCarsAndHolidays is on.
      // (A customer's own car bookings under My Bookings stay reachable.)
      {
        path: 'cars',
        title: 'Book Cars & Tempo Travellers with Driver | Ghumo Odisha',
        canActivate: [carsEnabledGuard],
        loadComponent: () => import('./features/cars/cars-page.component').then((m) => m.CarsPageComponent),
      },
      // No separate details page: a vehicle opens straight on its booking page (old links keep working).
      { path: 'cars/:id', redirectTo: 'cars/:id/book', pathMatch: 'full' },
      {
        path: 'cars/:id/book',
        title: 'Book your vehicle | Ghumo Odisha',
        canActivate: [carsEnabledGuard],
        loadComponent: () => import('./features/cars/car-booking.component').then((m) => m.CarBookingComponent),
      },
      {
        path: 'my-bookings/cars/:id',
        title: 'Car booking | Ghumo Odisha',
        canActivate: [customerGuard],
        loadComponent: () => import('./features/cars/car-booking-detail.component').then((m) => m.CarBookingDetailComponent),
      },
      {
        path: 'hotels',
        data: { title: 'Hotels' },
        canActivate: [carsEnabledGuard],
        loadComponent: () => import('./features/misc/coming-soon.component').then((m) => m.ComingSoonComponent),
      },
      { path: 'terms', title: 'Trip Terms & Conditions | Ghumo Odisha', loadComponent: () => import('./features/misc/terms.component').then((m) => m.TermsComponent) },
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
    // Public partner / influencer earnings page — sits under /admin but deliberately has no guard
    // and no admin layout: anyone with the link can open it and look up their own coupon code.
    path: 'admin/influencer',
    title: 'Partner earnings · Ghumo Odisha',
    loadComponent: () => import('./features/admin/influencer/influencer-portal.component').then((m) => m.InfluencerPortalComponent),
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
        path: 'destinations',
        loadComponent: () => import('./features/admin/destinations/destination-list.component').then((m) => m.DestinationListComponent),
      },
      {
        path: 'destinations/add',
        loadComponent: () => import('./features/admin/destinations/destination-form.component').then((m) => m.DestinationFormComponent),
      },
      {
        path: 'destinations/:id/edit',
        loadComponent: () => import('./features/admin/destinations/destination-form.component').then((m) => m.DestinationFormComponent),
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
        path: 'refunds',
        loadComponent: () => import('./features/admin/refunds/refund-list.component').then((m) => m.RefundListComponent),
      },
      {
        path: 'collections',
        title: 'Collections · Ghumo Odisha',
        loadComponent: () => import('./features/admin/collections/collections.component').then((m) => m.CollectionsComponent),
      },
      {
        path: 'customers',
        loadComponent: () => import('./features/admin/customers/customer-list.component').then((m) => m.CustomerListComponent),
      },
      {
        path: 'logs',
        loadComponent: () => import('./features/admin/logs/logs.component').then((m) => m.LogsComponent),
      },
      // Cars module: driver / car / pricing approvals and car bookings with their ₹ refunds.
      {
        path: 'drivers',
        loadComponent: () => import('./features/admin/cars/driver-list.component').then((m) => m.AdminDriverListComponent),
      },
      {
        path: 'drivers/:id',
        loadComponent: () => import('./features/admin/cars/driver-detail.component').then((m) => m.AdminDriverDetailComponent),
      },
      {
        path: 'cars',
        loadComponent: () => import('./features/admin/cars/car-list.component').then((m) => m.AdminCarListComponent),
      },
      {
        path: 'cars/new',
        loadComponent: () => import('./features/admin/cars/car-new.component').then((m) => m.AdminCarNewComponent),
      },
      {
        path: 'cars/pricing',
        loadComponent: () => import('./features/admin/cars/pricing-queue.component').then((m) => m.AdminPricingQueueComponent),
      },
      {
        path: 'cars/:id',
        loadComponent: () => import('./features/admin/cars/car-detail.component').then((m) => m.AdminCarDetailComponent),
      },
      {
        path: 'service-areas',
        loadComponent: () => import('./features/admin/cars/service-areas.component').then((m) => m.AdminServiceAreasComponent),
      },
      {
        path: 'car-bookings',
        loadComponent: () => import('./features/admin/cars/car-booking-list.component').then((m) => m.AdminCarBookingListComponent),
      },
      {
        path: 'car-bookings/:id',
        loadComponent: () =>
          import('./features/admin/cars/car-booking-detail.component').then((m) => m.AdminCarBookingDetailComponent),
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
  {
    path: 'driver/login',
    title: 'Driver sign in | Ghumo Odisha',
    loadComponent: () => import('./features/driver/driver-login.component').then((m) => m.DriverLoginComponent),
  },
  {
    // Driver area: own shell (bottom tab bar on phones), signed in with the Driver role.
    path: 'driver',
    title: 'Driver | Ghumo Odisha',
    canActivate: [driverGuard],
    loadComponent: () => import('./layouts/driver-layout/driver-layout.component').then((m) => m.DriverLayoutComponent),
    children: [
      { path: '', loadComponent: () => import('./features/driver/driver-home.component').then((m) => m.DriverHomeComponent) },
      { path: 'profile', loadComponent: () => import('./features/driver/driver-profile.component').then((m) => m.DriverProfileComponent) },
      { path: 'cars', loadComponent: () => import('./features/driver/driver-cars.component').then((m) => m.DriverCarsComponent) },
      { path: 'cars/:id', loadComponent: () => import('./features/driver/driver-car-form.component').then((m) => m.DriverCarFormComponent) },
      { path: 'bookings', loadComponent: () => import('./features/driver/driver-bookings.component').then((m) => m.DriverBookingsComponent) },
      { path: 'bookings/:id', loadComponent: () => import('./features/driver/driver-trip.component').then((m) => m.DriverTripComponent) },
      { path: 'earnings', loadComponent: () => import('./features/driver/driver-earnings.component').then((m) => m.DriverEarningsComponent) },
    ],
  },
  { path: '**', loadComponent: () => import('./features/misc/not-found.component').then((m) => m.NotFoundComponent) },
];
