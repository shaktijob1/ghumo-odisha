import { AdminBookingListItem } from './booking.model';

export interface DashboardCounters {
  totalTrips: number;
  activeTrips: number;
  totalCustomers: number;
  bookingRequests: number;
  confirmedBookings: number;
  upcomingTripsCount: number;
  confirmedRevenue: number;
  advanceCollected: number;
}

export interface DashboardUpcomingTrip {
  tripId: number;
  tripTitle: string;
  startDate: string;
  availableSeats: number;
  totalSeats: number;
}

export interface Dashboard {
  counters: DashboardCounters;
  upcomingTrips: DashboardUpcomingTrip[];
  recentRequests: AdminBookingListItem[];
  recentlyConfirmed: AdminBookingListItem[];
}
