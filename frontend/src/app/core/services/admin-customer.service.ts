import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';
import { AdminCustomerDetail, AdminCustomerListItem } from '../models/customer.model';

const base = () => `${environment.apiUrl}/admin/customers`;

@Injectable({ providedIn: 'root' })
export class AdminCustomerService {
  constructor(private readonly http: HttpClient) {}

  getCustomers(page: number, pageSize: number, search?: string): Observable<PagedResult<AdminCustomerListItem>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);

    return this.http.get<ApiResponse<PagedResult<AdminCustomerListItem>>>(base(), { params }).pipe(map((r) => r.data!));
  }

  getCustomer(id: number): Observable<AdminCustomerDetail> {
    return this.http.get<ApiResponse<AdminCustomerDetail>>(`${base()}/${id}`).pipe(map((r) => r.data!));
  }
}
