import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import {
  CompleteFollowUpRequest,
  CreateCrmCustomerRequest,
  CreateCrmDepartureRequest,
  CreateFollowUpRequest,
  CreateTripInterestRequest,
  CrmAdminUser,
  CrmChannel,
  CrmCommunication,
  CrmCommunicationFilter,
  CrmCustomerDetail,
  CrmCustomerFilter,
  CrmCustomerListItem,
  CrmDashboard,
  CrmDashboardFilter,
  CrmDeparture,
  CrmDepartureDetail,
  CrmDepartureFilter,
  CrmDuplicateCheck,
  CrmFollowUp,
  CrmFollowUpFilter,
  CrmFollowUpSummary,
  CrmImportPreview,
  CrmImportResult,
  CrmImportRow,
  CrmMonthlyReport,
  CrmNote,
  CrmPipeline,
  CrmRenderedMessage,
  CrmSourceReport,
  CrmStatus,
  CrmTemplate,
  CrmTemplateCategory,
  CrmTripInterest,
  CrmTripInterestFilter,
  CrmWhatsAppLink,
  LeadSource,
  SaveCrmTemplateRequest,
  UpdateCrmCustomerRequest,
  UpdateCrmDepartureDetailsRequest,
  UpdateTripInterestRequest,
} from '../models/crm.model';

const base = () => `${environment.apiUrl}/admin/crm`;

/** Query string from a filter object, leaving out empty values. */
function toParams(filter: object | undefined): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(filter ?? {})) {
    if (value !== null && value !== undefined && value !== '') params = params.set(key, String(value));
  }
  return params;
}

/** Admin → CRM. Every call goes to /api/admin/crm (Admin role only). */
@Injectable({ providedIn: 'root' })
export class AdminCrmService {
  private readonly http = inject(HttpClient);

  private get<T>(path: string, query?: object): Observable<T> {
    return this.http.get<ApiResponse<T>>(`${base()}/${path}`, { params: toParams(query) }).pipe(map((r) => r.data as T));
  }

  private post<T>(path: string, body: unknown = {}): Observable<T> {
    return this.http.post<ApiResponse<T>>(`${base()}/${path}`, body).pipe(map((r) => r.data as T));
  }

  private put<T>(path: string, body: unknown = {}, query?: object): Observable<T> {
    return this.http.put<ApiResponse<T>>(`${base()}/${path}`, body, { params: toParams(query) }).pipe(map((r) => r.data as T));
  }

  private delete(path: string): Observable<void> {
    return this.http.delete<ApiResponse<unknown>>(`${base()}/${path}`).pipe(map(() => undefined));
  }

  // ---------- Dashboard and reports ----------

  dashboard(filter: CrmDashboardFilter): Observable<CrmDashboard> {
    return this.get('dashboard', filter);
  }

  sourceReport(from: string | null, to: string | null): Observable<CrmSourceReport> {
    return this.get('reports/sources', { from, to });
  }

  monthlyReport(year: number, month: number): Observable<CrmMonthlyReport> {
    return this.get('reports/monthly', { year, month });
  }

  // ---------- Customers ----------

  customers(filter: CrmCustomerFilter): Observable<PagedResult<CrmCustomerListItem>> {
    return this.get('customers', filter);
  }

  customer(id: number): Observable<CrmCustomerDetail> {
    return this.get(`customers/${id}`);
  }

  checkPhone(phone: string, exceptCustomerId?: number | null): Observable<CrmDuplicateCheck> {
    return this.get('customers/check-phone', { phone, exceptCustomerId });
  }

  createCustomer(request: CreateCrmCustomerRequest): Observable<{ customerId: number }> {
    return this.post('customers', request);
  }

  updateCustomer(id: number, request: UpdateCrmCustomerRequest): Observable<void> {
    return this.put(`customers/${id}`, request);
  }

  changeCustomerStatus(id: number, status: CrmStatus, reason: string | null): Observable<void> {
    return this.put(`customers/${id}/status`, { status, reason });
  }

  addNote(customerId: number, text: string, customerTripInterestId: number | null): Observable<CrmNote> {
    return this.post(`customers/${customerId}/notes`, { text, customerTripInterestId });
  }

  admins(): Observable<CrmAdminUser[]> {
    return this.get('admins');
  }

  // ---------- Trip interests and pipeline ----------

  interests(filter: CrmTripInterestFilter): Observable<PagedResult<CrmTripInterest>> {
    return this.get('trip-interests', filter);
  }

  createInterest(request: CreateTripInterestRequest): Observable<{ customerTripInterestId: number }> {
    return this.post('trip-interests', request);
  }

  updateInterest(id: number, request: UpdateTripInterestRequest): Observable<void> {
    return this.put(`trip-interests/${id}`, request);
  }

  changeInterestStatus(id: number, status: CrmStatus, reason: string | null): Observable<void> {
    return this.put(`trip-interests/${id}/status`, { status, reason });
  }

  deleteInterest(id: number): Observable<void> {
    return this.delete(`trip-interests/${id}`);
  }

  pipeline(tripId: number | null, tripDateSlotId: number | null, source: LeadSource | null, itemsPerColumn = 30): Observable<CrmPipeline> {
    return this.get('pipeline', { tripId, tripDateSlotId, source, itemsPerColumn });
  }

  // ---------- Follow-ups ----------

  followUps(filter: CrmFollowUpFilter): Observable<PagedResult<CrmFollowUp>> {
    return this.get('follow-ups', filter);
  }

  followUpSummary(): Observable<CrmFollowUpSummary> {
    return this.get('follow-ups/summary');
  }

  createFollowUp(request: CreateFollowUpRequest): Observable<{ crmFollowUpId: number }> {
    return this.post('follow-ups', request);
  }

  completeFollowUp(id: number, request: CompleteFollowUpRequest): Observable<void> {
    return this.post(`follow-ups/${id}/complete`, request);
  }

  rescheduleFollowUp(id: number, dueDate: string, note: string | null): Observable<void> {
    return this.put(`follow-ups/${id}/reschedule`, { dueDate, note });
  }

  cancelFollowUp(id: number): Observable<void> {
    return this.post(`follow-ups/${id}/cancel`);
  }

  // ---------- WhatsApp templates and messages ----------

  templates(activeOnly = false, category: CrmTemplateCategory | null = null): Observable<CrmTemplate[]> {
    return this.get('whatsapp-templates', { activeOnly, category });
  }

  placeholders(): Observable<string[]> {
    return this.get('whatsapp-templates/placeholders');
  }

  createTemplate(request: SaveCrmTemplateRequest): Observable<CrmTemplate> {
    return this.post('whatsapp-templates', request);
  }

  updateTemplate(id: number, request: SaveCrmTemplateRequest): Observable<CrmTemplate> {
    return this.put(`whatsapp-templates/${id}`, request);
  }

  setTemplateActive(id: number, isActive: boolean): Observable<CrmTemplate> {
    return this.put(`whatsapp-templates/${id}/active`, {}, { isActive });
  }

  duplicateTemplate(id: number): Observable<CrmTemplate> {
    return this.post(`whatsapp-templates/${id}/duplicate`);
  }

  deleteTemplate(id: number): Observable<void> {
    return this.delete(`whatsapp-templates/${id}`);
  }

  previewMessage(customerId: number, customerTripInterestId: number | null, crmWhatsAppTemplateId: number | null, body: string | null): Observable<CrmRenderedMessage> {
    return this.post('messages/preview', { customerId, customerTripInterestId, crmWhatsAppTemplateId, body });
  }

  openWhatsApp(customerId: number, customerTripInterestId: number | null, crmWhatsAppTemplateId: number | null, message: string): Observable<CrmWhatsAppLink> {
    return this.post('messages/whatsapp', { customerId, customerTripInterestId, crmWhatsAppTemplateId, message });
  }

  markSent(communicationId: number): Observable<void> {
    return this.post(`messages/${communicationId}/mark-sent`);
  }

  markNotSent(communicationId: number): Observable<void> {
    return this.post(`messages/${communicationId}/mark-not-sent`);
  }

  logContact(customerId: number, customerTripInterestId: number | null, channel: CrmChannel, action: string, details: string | null): Observable<{ crmCommunicationId: number }> {
    return this.post('messages/log', { customerId, customerTripInterestId, channel, action, details });
  }

  communications(filter: CrmCommunicationFilter): Observable<PagedResult<CrmCommunication>> {
    return this.get('messages', filter);
  }

  // ---------- CSV import ----------

  importPreview(rows: CrmImportRow[], addInterestToExisting: boolean): Observable<CrmImportPreview> {
    return this.post('import/preview', { rows, addInterestToExisting });
  }

  importRows(rows: CrmImportRow[], addInterestToExisting: boolean): Observable<CrmImportResult> {
    return this.post('import', { rows, addInterestToExisting });
  }

  /** Adds any paid booking the CRM doesn't know about yet (normally automatic). */
  syncBookings(): Observable<{ linked: number }> {
    return this.post('sync-bookings');
  }

  // ---------- Departures ----------

  departures(filter: CrmDepartureFilter): Observable<PagedResult<CrmDeparture>> {
    return this.get('departures', filter);
  }

  departure(id: number): Observable<CrmDepartureDetail> {
    return this.get(`departures/${id}`);
  }

  createDeparture(request: CreateCrmDepartureRequest): Observable<{ tripDateSlotId: number }> {
    return this.post('departures', request);
  }

  updateDepartureDetails(id: number, request: UpdateCrmDepartureDetailsRequest): Observable<void> {
    return this.put(`departures/${id}/details`, request);
  }
}
