import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse, PagedResult } from '../models/api-response.model';

export type LogLevel = 'Information' | 'Warning' | 'Error' | 'Fatal';

export interface LogItem {
  appLogId: number;
  timestampUtc: string;
  level: LogLevel;
  message: string;
  source: string | null;
  requestId: string | null;
  requestMethod: string | null;
  requestPath: string | null;
  statusCode: number | null;
  elapsedMs: number | null;
  userId: number | null;
  userRole: 'Customer' | 'Admin' | null;
  userLabel: string | null;
  hasException: boolean;
}

export interface LogDetail {
  log: LogItem;
  exception: string | null;
  clientIp: string | null;
  propertiesJson: string | null;
  requestTrail: LogItem[];
}

export interface LogSummary {
  requests24h: number;
  failedRequests24h: number;
  warnings24h: number;
  errors24h: number;
  errors7d: number;
  topFailing: { method: string; path: string; count: number; lastAtUtc: string }[];
  recentErrors: LogItem[];
}

export interface AdminActivity {
  adminActivityId: number;
  createdAtUtc: string;
  adminUserId: number;
  adminName: string;
  action: string;
  area: string;
  targetId: number | null;
  httpMethod: string;
  path: string;
  statusCode: number;
  succeeded: boolean;
  requestId: string | null;
}

export interface LogQuery {
  search?: string;
  level?: string;
  from?: string;
  to?: string;
  page: number;
  pageSize: number;
}

const base = () => `${environment.apiUrl}/admin/logs`;

@Injectable({ providedIn: 'root' })
export class AdminLogsService {
  constructor(private readonly http: HttpClient) {}

  summary(): Observable<LogSummary> {
    return this.http.get<ApiResponse<LogSummary>>(`${base()}/summary`).pipe(map((r) => r.data!));
  }

  list(q: LogQuery): Observable<PagedResult<LogItem>> {
    let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize);
    if (q.search) params = params.set('search', q.search);
    if (q.level) params = params.set('level', q.level);
    if (q.from) params = params.set('from', q.from);
    if (q.to) params = params.set('to', q.to);
    return this.http.get<ApiResponse<PagedResult<LogItem>>>(base(), { params }).pipe(map((r) => r.data!));
  }

  detail(id: number): Observable<LogDetail> {
    return this.http.get<ApiResponse<LogDetail>>(`${base()}/${id}`).pipe(map((r) => r.data!));
  }

  activity(search: string, page: number, pageSize: number): Observable<PagedResult<AdminActivity>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    return this.http.get<ApiResponse<PagedResult<AdminActivity>>>(`${base()}/activity`, { params }).pipe(map((r) => r.data!));
  }
}
