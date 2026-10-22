import { BookingSource, BookingStatus, TripDateSlotStatus } from './enums.model';

// Mirrors GhumoOdisha.Domain.Enums / Application.Crm.Dtos (enums travel as numbers).

export enum LeadSource {
  Instagram = 0,
  Facebook = 1,
  WhatsApp = 2,
  Website = 3,
  Referral = 4,
  Google = 5,
  WalkIn = 6,
  Phone = 7,
  Other = 8,
}

export const LeadSourceLabels: Record<LeadSource, string> = {
  [LeadSource.Instagram]: 'Instagram',
  [LeadSource.Facebook]: 'Facebook',
  [LeadSource.WhatsApp]: 'WhatsApp',
  [LeadSource.Website]: 'Website',
  [LeadSource.Referral]: 'Referral',
  [LeadSource.Google]: 'Google',
  [LeadSource.WalkIn]: 'Walk-in',
  [LeadSource.Phone]: 'Phone call',
  [LeadSource.Other]: 'Other',
};

export const LeadSources: LeadSource[] = Object.values(LeadSource).filter((v): v is LeadSource => typeof v === 'number');

export enum CrmStatus {
  NewLead = 0,
  Interested = 1,
  HotLead = 2,
  FollowUp = 3,
  BookingPending = 4,
  Booked = 5,
  Completed = 6,
  Cancelled = 7,
  NotInterested = 8,
  Later = 9,
  RepeatCustomer = 10,
}

export const CrmStatusLabels: Record<CrmStatus, string> = {
  [CrmStatus.NewLead]: 'New lead',
  [CrmStatus.Interested]: 'Interested',
  [CrmStatus.HotLead]: 'Hot lead',
  [CrmStatus.FollowUp]: 'Follow-up',
  [CrmStatus.BookingPending]: 'Booking pending',
  [CrmStatus.Booked]: 'Booked',
  [CrmStatus.Completed]: 'Completed',
  [CrmStatus.Cancelled]: 'Cancelled',
  [CrmStatus.NotInterested]: 'Not interested',
  [CrmStatus.Later]: 'Later',
  [CrmStatus.RepeatCustomer]: 'Repeat customer',
};

export const CrmStatuses: CrmStatus[] = Object.values(CrmStatus).filter((v): v is CrmStatus => typeof v === 'number');

/** Stages an admin can pick for an enquiry — Booked / Completed / Repeat follow real bookings. */
export const CrmEnquiryStatuses: CrmStatus[] = CrmStatuses.filter(
  (s) => s !== CrmStatus.Booked && s !== CrmStatus.Completed && s !== CrmStatus.RepeatCustomer,
);

/** Badge colour per stage (classes from the shared .badge styles; '' = neutral grey). */
export const CrmStatusTone: Record<CrmStatus, '' | 'ok' | 'wait' | 'bad' | 'info'> = {
  [CrmStatus.NewLead]: 'info',
  [CrmStatus.Interested]: 'info',
  [CrmStatus.HotLead]: 'bad',
  [CrmStatus.FollowUp]: 'wait',
  [CrmStatus.BookingPending]: 'wait',
  [CrmStatus.Booked]: 'ok',
  [CrmStatus.Completed]: 'ok',
  [CrmStatus.Cancelled]: '',
  [CrmStatus.NotInterested]: '',
  [CrmStatus.Later]: '',
  [CrmStatus.RepeatCustomer]: 'ok',
};

export enum CrmFollowUpStatus {
  Pending = 0,
  Done = 1,
  Cancelled = 2,
}

export enum CrmFollowUpBucket {
  Today = 0,
  Overdue = 1,
  Upcoming = 2,
  Pending = 3,
  Done = 4,
}

export enum CrmChannel {
  WhatsApp = 0,
  Phone = 1,
  Email = 2,
  Other = 3,
}

export const CrmChannelLabels: Record<CrmChannel, string> = {
  [CrmChannel.WhatsApp]: 'WhatsApp',
  [CrmChannel.Phone]: 'Phone',
  [CrmChannel.Email]: 'Email',
  [CrmChannel.Other]: 'Other',
};

export enum CrmMessageStatus {
  WhatsAppOpened = 0,
  SentManually = 1,
  NotConfirmed = 2,
  Logged = 3,
}

/** Honest wording: without the WhatsApp API the CRM never knows about delivery or reads. */
export const CrmMessageStatusLabels: Record<CrmMessageStatus, string> = {
  [CrmMessageStatus.WhatsAppOpened]: 'Opened WhatsApp',
  [CrmMessageStatus.SentManually]: 'Sent manually',
  [CrmMessageStatus.NotConfirmed]: 'Not sent',
  [CrmMessageStatus.Logged]: 'Logged',
};

export enum CrmTemplateCategory {
  NewEnquiry = 0,
  Itinerary = 1,
  FollowUp = 2,
  EarlyBooking = 3,
  LimitedSeats = 4,
  TripConfirmed = 5,
  PaymentReminder = 6,
  TripReminder = 7,
  BalancePayment = 8,
  TripCompleted = 9,
  ReviewRequest = 10,
  Referral = 11,
  Custom = 12,
}

export const CrmTemplateCategoryLabels: Record<CrmTemplateCategory, string> = {
  [CrmTemplateCategory.NewEnquiry]: 'New enquiry',
  [CrmTemplateCategory.Itinerary]: 'Itinerary',
  [CrmTemplateCategory.FollowUp]: 'Follow-up',
  [CrmTemplateCategory.EarlyBooking]: 'Early booking',
  [CrmTemplateCategory.LimitedSeats]: 'Limited seats',
  [CrmTemplateCategory.TripConfirmed]: 'Trip confirmed',
  [CrmTemplateCategory.PaymentReminder]: 'Payment reminder',
  [CrmTemplateCategory.TripReminder]: 'Trip reminder',
  [CrmTemplateCategory.BalancePayment]: 'Balance payment',
  [CrmTemplateCategory.TripCompleted]: 'Trip completed',
  [CrmTemplateCategory.ReviewRequest]: 'Review request',
  [CrmTemplateCategory.Referral]: 'Referral',
  [CrmTemplateCategory.Custom]: 'Custom',
};

export enum CrmDepartureStatus {
  Draft = 0,
  OpenForBooking = 1,
  FillingFast = 2,
  Confirmed = 3,
  AlmostFull = 4,
  SoldOut = 5,
  Completed = 6,
}

export const CrmDepartureStatusLabels: Record<CrmDepartureStatus, string> = {
  [CrmDepartureStatus.Draft]: 'Draft',
  [CrmDepartureStatus.OpenForBooking]: 'Open for booking',
  [CrmDepartureStatus.FillingFast]: 'Filling fast',
  [CrmDepartureStatus.Confirmed]: 'Trip confirmed',
  [CrmDepartureStatus.AlmostFull]: 'Almost full',
  [CrmDepartureStatus.SoldOut]: 'Sold out',
  [CrmDepartureStatus.Completed]: 'Completed',
};

export const CrmDepartureStatusTone: Record<CrmDepartureStatus, '' | 'ok' | 'wait' | 'bad' | 'info'> = {
  [CrmDepartureStatus.Draft]: '',
  [CrmDepartureStatus.OpenForBooking]: 'info',
  [CrmDepartureStatus.FillingFast]: 'wait',
  [CrmDepartureStatus.Confirmed]: 'ok',
  [CrmDepartureStatus.AlmostFull]: 'bad',
  [CrmDepartureStatus.SoldOut]: '',
  [CrmDepartureStatus.Completed]: '',
};

// ---------- Customers ----------

export interface CrmCustomerFilter {
  search?: string | null;
  status?: CrmStatus | null;
  source?: LeadSource | null;
  tripId?: number | null;
  tripDateSlotId?: number | null;
  assignedAdminUserId?: number | null;
  year?: number | null;
  month?: number | null;
  activeOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export interface CrmCustomerListItem {
  customerId: number;
  name: string;
  phoneNumber: string | null;
  email: string | null;
  city: string | null;
  leadSource: LeadSource;
  status: CrmStatus;
  createdAt: string;
  lastContactAt: string | null;
  nextFollowUpDate: string | null;
  assignedAdminUserId: number | null;
  assignedAdminName: string | null;
  interestCount: number;
  totalBookings: number;
  totalTrips: number;
  totalSpent: number;
  isRegistered: boolean;
}

export interface CrmCustomerBooking {
  bookingId: number;
  reference: string;
  tripId: number;
  tripTitle: string;
  startDate: string;
  seats: number;
  status: BookingStatus;
  source: BookingSource;
  total: number;
  paid: number;
  balance: number;
  createdAt: string;
}

export interface CrmCustomerDetail {
  customerId: number;
  name: string;
  phoneNumber: string | null;
  alternatePhoneNumber: string | null;
  email: string | null;
  whatsAppAvailable: boolean;
  city: string | null;
  leadSource: LeadSource;
  status: CrmStatus;
  summary: string | null;
  createdAt: string;
  lastContactAt: string | null;
  nextFollowUpDate: string | null;
  assignedAdminUserId: number | null;
  assignedAdminName: string | null;
  isRegistered: boolean;
  totalBookings: number;
  totalTrips: number;
  totalSpent: number;
  interests: CrmTripInterest[];
  bookings: CrmCustomerBooking[];
  followUps: CrmFollowUp[];
  notes: CrmNote[];
  communications: CrmCommunication[];
  statusHistory: CrmStatusChange[];
}

export interface CrmDuplicateCheck {
  exists: boolean;
  customerId: number | null;
  name: string | null;
  phoneNumber: string | null;
  status: CrmStatus | null;
  interestCount: number;
  totalBookings: number;
  matchedAlternate: boolean;
}

export interface CrmInterestInput {
  tripId: number;
  tripDateSlotId: number | null;
  travellers: number;
  status: CrmStatus;
  notes: string | null;
  expectedAmount: number | null;
}

export interface CreateCrmCustomerRequest {
  name: string;
  phoneNumber: string;
  alternatePhoneNumber: string | null;
  email: string | null;
  whatsAppAvailable: boolean;
  city: string | null;
  leadSource: LeadSource;
  status: CrmStatus;
  summary: string | null;
  assignedAdminUserId: number | null;
  interest: CrmInterestInput | null;
  nextFollowUpDate: string | null;
  followUpNote: string | null;
}

export interface UpdateCrmCustomerRequest {
  name: string;
  phoneNumber: string;
  alternatePhoneNumber: string | null;
  email: string | null;
  whatsAppAvailable: boolean;
  city: string | null;
  leadSource: LeadSource;
  summary: string | null;
  assignedAdminUserId: number | null;
}

export interface CrmNote {
  crmNoteId: number;
  text: string;
  customerTripInterestId: number | null;
  adminName: string | null;
  createdAt: string;
}

export interface CrmStatusChange {
  crmStatusChangeId: number;
  customerTripInterestId: number | null;
  tripTitle: string | null;
  fromStatus: CrmStatus | null;
  toStatus: CrmStatus;
  reason: string | null;
  adminName: string | null;
  createdAt: string;
}

export interface CrmAdminUser {
  adminUserId: number;
  name: string;
}

// ---------- Trip interests ----------

export interface CrmTripInterest {
  customerTripInterestId: number;
  customerId: number;
  customerName: string;
  customerPhone: string | null;
  customerSource: LeadSource;
  tripId: number;
  tripTitle: string;
  tripDateSlotId: number | null;
  startDate: string | null;
  endDate: string | null;
  travellers: number;
  status: CrmStatus;
  interestedAt: string;
  lastContactAt: string | null;
  nextFollowUpDate: string | null;
  notes: string | null;
  expectedAmount: number | null;
  bookingId: number | null;
  bookingReference: string | null;
  bookingTotal: number | null;
  bookingPaid: number | null;
  lostReason: string | null;
}

export interface CrmTripInterestFilter {
  tripId?: number | null;
  tripDateSlotId?: number | null;
  status?: CrmStatus | null;
  source?: LeadSource | null;
  year?: number | null;
  month?: number | null;
  search?: string | null;
  page?: number;
  pageSize?: number;
}

export interface CreateTripInterestRequest {
  customerId: number;
  tripId: number;
  tripDateSlotId: number | null;
  travellers: number;
  status: CrmStatus;
  notes: string | null;
  expectedAmount: number | null;
  nextFollowUpDate: string | null;
}

export interface UpdateTripInterestRequest {
  tripId: number;
  tripDateSlotId: number | null;
  travellers: number;
  notes: string | null;
  expectedAmount: number | null;
  lostReason: string | null;
}

export interface CrmPipelineColumn {
  status: CrmStatus;
  count: number;
  items: CrmTripInterest[];
}

export interface CrmPipeline {
  columns: CrmPipelineColumn[];
}

// ---------- Follow-ups ----------

export interface CrmFollowUp {
  crmFollowUpId: number;
  customerId: number;
  customerName: string;
  customerPhone: string | null;
  customerStatus: CrmStatus;
  customerTripInterestId: number | null;
  tripTitle: string | null;
  departureDate: string | null;
  interestStatus: CrmStatus | null;
  dueDate: string;
  daysOverdue: number;
  note: string | null;
  status: CrmFollowUpStatus;
  outcome: string | null;
  completedAt: string | null;
  assignedAdminUserId: number | null;
  assignedAdminName: string | null;
}

export interface CrmFollowUpSummary {
  today: number;
  overdue: number;
  upcoming: number;
}

export interface CrmFollowUpFilter {
  bucket?: CrmFollowUpBucket;
  assignedAdminUserId?: number | null;
  tripId?: number | null;
  customerId?: number | null;
  page?: number;
  pageSize?: number;
}

export interface CreateFollowUpRequest {
  customerId: number;
  customerTripInterestId: number | null;
  dueDate: string;
  note: string | null;
  assignedAdminUserId: number | null;
}

export interface CompleteFollowUpRequest {
  outcome: string | null;
  nextDueDate: string | null;
  nextNote: string | null;
  newStatus: CrmStatus | null;
}

// ---------- Templates and messages ----------

export interface CrmTemplate {
  crmWhatsAppTemplateId: number;
  name: string;
  category: CrmTemplateCategory;
  body: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface SaveCrmTemplateRequest {
  name: string;
  category: CrmTemplateCategory;
  body: string;
  isActive: boolean;
}

export interface CrmRenderedMessage {
  message: string;
  templateName: string | null;
  customerName: string;
  whatsAppPhone: string | null;
  whatsAppAvailable: boolean;
  missingValues: string[];
}

export interface CrmWhatsAppLink {
  crmCommunicationId: number;
  appUrl: string;
  webUrl: string;
}

export interface CrmCommunication {
  crmCommunicationId: number;
  customerId: number;
  customerName: string;
  customerPhone: string | null;
  customerTripInterestId: number | null;
  tripTitle: string | null;
  channel: CrmChannel;
  status: CrmMessageStatus;
  action: string;
  templateName: string | null;
  messagePreview: string | null;
  adminName: string | null;
  createdAt: string;
  sentConfirmedAt: string | null;
}

export interface CrmCommunicationFilter {
  customerId?: number | null;
  /** Customer name or mobile number. */
  search?: string | null;
  tripId?: number | null;
  channel?: CrmChannel | null;
  status?: CrmMessageStatus | null;
  from?: string | null;
  to?: string | null;
  adminUserId?: number | null;
  page?: number;
  pageSize?: number;
}

// ---------- Departures ----------

export interface CrmDeparture {
  tripDateSlotId: number;
  tripId: number;
  tripTitle: string;
  startDate: string;
  endDate: string;
  slotStatus: TripDateSlotStatus;
  vehicle: string | null;
  regularPrice: number;
  pricePerPerson: number | null;
  earlyBirdPrice: number | null;
  earlyBirdEndsOn: string | null;
  currentPrice: number;
  isEarlyBird: boolean;
  bookingAmountPerSeat: number;
  totalSeats: number;
  bookedSeats: number;
  availableSeats: number;
  minimumBookedSeats: number | null;
  minimumReached: boolean;
  campaignStatus: CrmDepartureStatus;
  leads: number;
  interested: number;
  hotLeads: number;
  bookingPending: number;
  booked: number;
  cancelled: number;
}

export interface CrmDepartureBooking {
  bookingId: number;
  reference: string;
  customerId: number;
  customerName: string;
  customerPhone: string | null;
  customerSource: LeadSource;
  seats: number;
  status: BookingStatus;
  source: BookingSource;
  total: number;
  paid: number;
  balance: number;
  createdAt: string;
}

export interface CrmDepartureDetail {
  departure: CrmDeparture;
  interests: CrmTripInterest[];
  bookings: CrmDepartureBooking[];
}

export interface CrmDepartureFilter {
  tripId?: number | null;
  year?: number | null;
  month?: number | null;
  includePast?: boolean;
  page?: number;
  pageSize?: number;
}

export interface CreateCrmDepartureRequest {
  tripId: number;
  startDate: string;
  endDate: string;
  totalSeats: number;
  openForBooking: boolean;
  vehicle: string | null;
  minimumBookedSeats: number | null;
  pricePerPerson: number | null;
  earlyBirdPrice: number | null;
  earlyBirdEndsOn: string | null;
}

export interface UpdateCrmDepartureDetailsRequest {
  vehicle: string | null;
  minimumBookedSeats: number | null;
  pricePerPerson: number | null;
  earlyBirdPrice: number | null;
  earlyBirdEndsOn: string | null;
}

// ---------- Dashboard and reports ----------

export interface CrmDashboardFilter {
  year?: number | null;
  month?: number | null;
  tripId?: number | null;
  tripDateSlotId?: number | null;
  source?: LeadSource | null;
  status?: CrmStatus | null;
}

export interface CrmDashboard {
  totalCustomers: number;
  totalLeads: number;
  newLeads: number;
  interested: number;
  hotLeads: number;
  followUp: number;
  bookingPending: number;
  booked: number;
  completed: number;
  cancelled: number;
  notInterested: number;
  later: number;
  repeatCustomers: number;
  followUpsToday: number;
  followUpsOverdue: number;
  followUpsUpcoming: number;
  conversionRate: number;
  todayFollowUps: CrmFollowUp[];
  upcomingDepartures: CrmDeparture[];
}

export interface CrmSourceRow {
  source: LeadSource;
  leads: number;
  bookings: number;
  bookedValue: number;
  conversionRate: number;
}

export interface CrmSourceReport {
  from: string | null;
  to: string | null;
  rows: CrmSourceRow[];
}

export interface CrmTopTrip {
  tripId: number;
  tripTitle: string;
  bookings: number;
  seats: number;
  bookedValue: number;
}

export interface CrmMonthlyReport {
  year: number;
  month: number;
  totalLeads: number;
  totalBookings: number;
  seatsBooked: number;
  bookedValue: number;
  paymentsReceived: number;
  conversionRate: number;
  cancelled: number;
  completed: number;
  topTrips: CrmTopTrip[];
  topSources: CrmSourceRow[];
}

// ---------- CSV import ----------

export interface CrmImportRow {
  name: string | null;
  mobile: string | null;
  email: string | null;
  city: string | null;
  trip: string | null;
  departure: string | null;
  status: string | null;
  source: string | null;
  notes: string | null;
  travellers: number | null;
}

export enum CrmImportOutcome {
  New = 0,
  Existing = 1,
  SameAsEarlierRow = 2,
  Invalid = 3,
}

export interface CrmImportRowResult {
  row: number;
  outcome: CrmImportOutcome;
  name: string | null;
  mobile: string | null;
  existingCustomerId: number | null;
  existingCustomerName: string | null;
  tripId: number | null;
  tripTitle: string | null;
  tripDateSlotId: number | null;
  status: CrmStatus;
  source: LeadSource;
  errors: string[];
  warnings: string[];
}

export interface CrmImportPreview {
  total: number;
  new: number;
  existing: number;
  invalid: number;
  rows: CrmImportRowResult[];
}

export interface CrmImportResult {
  customersCreated: number;
  interestsAdded: number;
  skipped: number;
  invalid: number;
  errors: string[];
}
