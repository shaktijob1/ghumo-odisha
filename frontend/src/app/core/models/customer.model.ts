export interface AdminCustomerListItem {
  customerId: number;
  name: string;
  phoneNumber: string;
  email: string | null;
  tripCount: number;
  confirmedBookingCount: number;
  totalAmount: number;
  advancePaid: number;
  remaining: number;
}

export interface AdminCustomerDetail {
  customerId: number;
  name: string;
  phoneNumber: string;
  email: string | null;
  isVerified: boolean;
  createdAt: string;
  lastLoginAt: string | null;
  totalBookings: number;
  confirmedBookingCount: number;
  totalAmount: number;
  advancePaid: number;
  remaining: number;
}
