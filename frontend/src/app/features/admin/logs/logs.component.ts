import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  AdminActivity,
  AdminLogsService,
  LogDetail,
  LogItem,
  LogSummary,
} from '../../../core/services/admin-logs.service';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { ToastService } from '../../../core/services/toast.service';

type Tab = 'overview' | 'logs' | 'activity';
type LoadState = 'loading' | 'ready' | 'error';

const PAGE_SIZE = 50;

/**
 * Admin Logs: overview → searchable log list → one request's full story → the raw error.
 * Search accepts a customer's "Error ref", their phone number, or any text.
 */
@Component({
  selector: 'app-admin-logs',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent],
  templateUrl: './logs.component.html',
})
export class LogsComponent implements OnInit {
  private readonly logsService = inject(AdminLogsService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly tab = signal<Tab>('overview');

  // Overview
  readonly summaryState = signal<LoadState>('loading');
  readonly summary = signal<LogSummary | null>(null);

  // Log list
  readonly listState = signal<LoadState>('loading');
  readonly logs = signal<LogItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));
  search = '';
  level = '';
  fromDate = '';
  toDate = '';

  // Detail
  readonly detail = signal<LogDetail | null>(null);
  readonly detailLoading = signal(false);

  // Admin activity
  readonly activityState = signal<LoadState>('loading');
  readonly activity = signal<AdminActivity[]>([]);
  readonly activityTotal = signal(0);
  readonly activityPage = signal(1);
  readonly activityPageCount = computed(() => Math.max(1, Math.ceil(this.activityTotal() / PAGE_SIZE)));
  activitySearch = '';

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    if (q.get('search')) {
      this.search = q.get('search')!;
      this.showTab('logs');
    } else {
      this.showTab('overview');
    }
  }

  showTab(tab: Tab): void {
    this.tab.set(tab);
    if (tab === 'overview') this.loadSummary();
    if (tab === 'logs') this.loadLogs();
    if (tab === 'activity') this.loadActivity();
  }

  loadSummary(): void {
    this.summaryState.set('loading');
    this.logsService.summary().subscribe({
      next: (s) => {
        this.summary.set(s);
        this.summaryState.set('ready');
      },
      error: () => this.summaryState.set('error'),
    });
  }

  runSearch(): void {
    this.page.set(1);
    this.router.navigate([], { queryParams: { search: this.search.trim() || null }, replaceUrl: true });
    this.loadLogs();
  }

  clearFilters(): void {
    this.search = '';
    this.level = '';
    this.fromDate = '';
    this.toDate = '';
    this.runSearch();
  }

  /** Jump from anywhere (overview, activity) to all log lines of one request. */
  showRequest(requestId: string | null): void {
    if (!requestId) return;
    this.search = requestId;
    this.level = '';
    this.fromDate = '';
    this.toDate = '';
    this.tab.set('logs');
    this.runSearch();
  }

  showFailuresFor(path: string): void {
    this.search = path;
    this.level = 'Error';
    this.tab.set('logs');
    this.runSearch();
  }

  loadLogs(): void {
    this.listState.set('loading');
    this.logsService
      .list({
        search: this.search.trim() || undefined,
        level: this.level || undefined,
        // Local calendar days → the API filters on UTC instants.
        from: this.fromDate ? new Date(`${this.fromDate}T00:00:00`).toISOString() : undefined,
        to: this.toDate ? new Date(`${this.toDate}T23:59:59.999`).toISOString() : undefined,
        page: this.page(),
        pageSize: PAGE_SIZE,
      })
      .subscribe({
        next: (r) => {
          this.logs.set(r.items);
          this.total.set(r.totalCount);
          this.listState.set('ready');
        },
        error: () => this.listState.set('error'),
      });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.pageCount()) return;
    this.page.set(page);
    this.loadLogs();
  }

  openLog(item: LogItem): void {
    this.detailLoading.set(true);
    this.detail.set(null);
    this.logsService.detail(item.appLogId).subscribe({
      next: (d) => {
        this.detail.set(d);
        this.detailLoading.set(false);
      },
      error: () => this.detailLoading.set(false),
    });
  }

  closeDetail(): void {
    this.detail.set(null);
    this.detailLoading.set(false);
  }

  loadActivity(): void {
    this.activityState.set('loading');
    this.logsService.activity(this.activitySearch.trim(), this.activityPage(), PAGE_SIZE).subscribe({
      next: (r) => {
        this.activity.set(r.items);
        this.activityTotal.set(r.totalCount);
        this.activityState.set('ready');
      },
      error: () => this.activityState.set('error'),
    });
  }

  goToActivityPage(page: number): void {
    if (page < 1 || page > this.activityPageCount()) return;
    this.activityPage.set(page);
    this.loadActivity();
  }

  copy(text: string | null | undefined, what: string): void {
    if (!text) return;
    navigator.clipboard?.writeText(text).then(
      () => this.toast.success(`${what} copied.`),
      () => this.toast.info(text),
    );
  }

  // ---------- display helpers ----------

  levelClass(level: string): string {
    return level === 'Error' || level === 'Fatal' ? 'bad' : level === 'Warning' ? 'wait' : 'info';
  }

  levelLabel(level: string): string {
    return level === 'Information' ? 'Info' : level;
  }

  statusClass(status: number | null): string {
    if (status === null) return '';
    return status >= 500 ? 'bad' : status >= 400 ? 'wait' : 'ok';
  }

  /** Where "who" links to: the customer's page for customers; nothing for admins. */
  userLink(item: LogItem): (string | number)[] | null {
    return item.userRole === 'Customer' && item.userId ? ['/admin/customers', item.userId] : null;
  }

  /** Milliseconds after the first line of the request — shows the order and timing of each step. */
  offsetMs(item: LogItem, trail: LogItem[]): number {
    if (!trail.length) return 0;
    return Math.max(0, new Date(item.timestampUtc).getTime() - new Date(trail[0].timestampUtc).getTime());
  }

  prettyJson(json: string | null): string | null {
    if (!json) return null;
    try {
      return JSON.stringify(JSON.parse(json), null, 2);
    } catch {
      return json;
    }
  }

  /** Booking-related admin activity links straight to the booking. */
  activityLink(a: AdminActivity): (string | number)[] | null {
    if (!a.targetId) return null;
    if (a.area === 'Bookings') return ['/admin/bookings', a.targetId];
    if (a.area === 'Trips') return ['/admin/trips', a.targetId];
    return null;
  }
}
