import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { forkJoin, of, catchError } from 'rxjs';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { CrmMonthlyReport, CrmSourceRow, LeadSourceLabels } from '../../../../core/models/crm.model';
import { StatePanelComponent } from '../../../../shared/components/state-panel.component';
import { toLocalDateKey } from '../../../../shared/utils/date-key';

const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

type RangePreset = 'month' | '3months' | 'year' | 'all' | 'custom';

/**
 * Admin → CRM → Reports: a monthly report for any month and year, leads / bookings / conversion by
 * lead source for any date range, and the last six months side by side. Leads are trip enquiries;
 * bookings and money come from real bookings and payments.
 */
@Component({
  selector: 'app-crm-reports',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent],
  templateUrl: './crm-reports.component.html',
  styleUrls: ['../crm-shared.css', './crm-reports.component.css'],
})
export class CrmReportsComponent implements OnInit {
  private readonly crm = inject(AdminCrmService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly monthNames = MONTH_NAMES;
  readonly sourceLabels = LeadSourceLabels;
  readonly years = (() => {
    const now = new Date().getFullYear();
    return Array.from({ length: 6 }, (_, i) => now + 1 - i);
  })();

  // Monthly report
  year = new Date().getFullYear();
  month = new Date().getMonth() + 1;
  readonly monthState = signal<'loading' | 'ready' | 'error'>('loading');
  readonly monthly = signal<CrmMonthlyReport | null>(null);

  // Lead sources
  preset: RangePreset = 'year';
  from = '';
  to = '';
  readonly sourceState = signal<'loading' | 'ready' | 'error'>('loading');
  readonly sources = signal<CrmSourceRow[]>([]);

  // Last six months
  readonly trend = signal<CrmMonthlyReport[]>([]);

  readonly sourceRows = computed(() => [...this.sources()].filter((r) => r.leads > 0 || r.bookings > 0).sort((a, b) => b.leads - a.leads || b.bookings - a.bookings));
  readonly sourceTotals = computed(() => {
    const rows = this.sources();
    const leads = rows.reduce((n, r) => n + r.leads, 0);
    const bookings = rows.reduce((n, r) => n + r.bookings, 0);
    return { leads, bookings, value: rows.reduce((n, r) => n + r.bookedValue, 0), conversion: leads ? Math.round((bookings * 10000) / leads) / 100 : 0 };
  });
  /** The source that converts best, among those with enough leads to mean something. */
  readonly bestSource = computed(() => {
    const candidates = this.sourceRows().filter((r) => r.leads >= 5);
    return candidates.length ? candidates.reduce((best, r) => (r.conversionRate > best.conversionRate ? r : best)) : null;
  });
  readonly maxLeads = computed(() => Math.max(1, ...this.sourceRows().map((r) => r.leads)));
  readonly maxTrendValue = computed(() => Math.max(1, ...this.trend().map((m) => m.totalLeads), ...this.trend().map((m) => m.totalBookings)));

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    const y = Number(q.get('year'));
    const m = Number(q.get('month'));
    if (y >= 2000 && y <= 2100) this.year = y;
    if (m >= 1 && m <= 12) this.month = m;
    this.loadMonth();
    this.setPreset('year');
    this.loadTrend();
  }

  get monthLabel(): string {
    return `${MONTH_NAMES[this.month - 1]} ${this.year}`;
  }

  stepMonth(delta: number): void {
    const d = new Date(this.year, this.month - 1 + delta, 1);
    this.year = d.getFullYear();
    this.month = d.getMonth() + 1;
    this.loadMonth();
  }

  loadMonth(): void {
    this.router.navigate([], { relativeTo: this.route, replaceUrl: true, queryParams: { year: this.year, month: this.month } });
    this.monthState.set('loading');
    this.crm.monthlyReport(this.year, this.month).subscribe({
      next: (r) => {
        this.monthly.set(r);
        this.monthState.set('ready');
      },
      error: () => this.monthState.set('error'),
    });
  }

  setPreset(preset: RangePreset): void {
    this.preset = preset;
    const now = new Date();
    if (preset === 'month') {
      this.from = toLocalDateKey(new Date(now.getFullYear(), now.getMonth(), 1));
      this.to = toLocalDateKey(now);
    } else if (preset === '3months') {
      this.from = toLocalDateKey(new Date(now.getFullYear(), now.getMonth() - 2, 1));
      this.to = toLocalDateKey(now);
    } else if (preset === 'year') {
      this.from = toLocalDateKey(new Date(now.getFullYear(), 0, 1));
      this.to = toLocalDateKey(now);
    } else if (preset === 'all') {
      this.from = this.to = '';
    }
    if (preset !== 'custom') this.loadSources();
  }

  loadSources(): void {
    this.sourceState.set('loading');
    this.crm.sourceReport(this.from || null, this.to || null).subscribe({
      next: (r) => {
        this.sources.set(r.rows);
        this.sourceState.set('ready');
      },
      error: () => this.sourceState.set('error'),
    });
  }

  /** The six months up to this one, oldest first. */
  private loadTrend(): void {
    const now = new Date();
    const months = Array.from({ length: 6 }, (_, i) => new Date(now.getFullYear(), now.getMonth() - 5 + i, 1));
    forkJoin(months.map((d) => this.crm.monthlyReport(d.getFullYear(), d.getMonth() + 1).pipe(catchError(() => of(null))))).subscribe((list) =>
      this.trend.set(list.filter((m): m is CrmMonthlyReport => m !== null)),
    );
  }

  openMonth(m: CrmMonthlyReport): void {
    this.year = m.year;
    this.month = m.month;
    this.loadMonth();
  }

  shortMonth(m: CrmMonthlyReport): string {
    return `${MONTH_NAMES[m.month - 1].slice(0, 3)} ${String(m.year).slice(2)}`;
  }
}
