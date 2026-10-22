import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, debounceTime } from 'rxjs';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { AdminTripService } from '../../../../core/services/admin-trip.service';
import { AdminTripListItem } from '../../../../core/models/trip.model';
import {
  CrmAdminUser,
  CrmCustomerListItem,
  CrmStatus,
  CrmStatusLabels,
  CrmStatusTone,
  CrmStatuses,
  LeadSource,
  LeadSourceLabels,
  LeadSources,
} from '../../../../core/models/crm.model';
import { StatePanelComponent } from '../../../../shared/components/state-panel.component';
import { dueState, formatPhone } from '../crm-utils';

function num(value: string | null): number | null {
  if (value === null || value === '') return null;
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

const MONTH_NAMES = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];

@Component({
  selector: 'app-crm-customer-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent],
  templateUrl: './crm-customer-list.component.html',
  styleUrls: ['../crm-shared.css'],
})
export class CrmCustomerListComponent implements OnInit, OnDestroy {
  private readonly crm = inject(AdminCrmService);
  private readonly tripService = inject(AdminTripService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly searchChanges = new Subject<void>();

  readonly statusLabels = CrmStatusLabels;
  readonly statusTone = CrmStatusTone;
  readonly statuses = CrmStatuses;
  readonly sourceLabels = LeadSourceLabels;
  readonly sources = LeadSources;
  readonly monthNames = MONTH_NAMES;
  readonly years = (() => {
    const now = new Date().getFullYear();
    return Array.from({ length: 6 }, (_, i) => now + 1 - i);
  })();
  readonly dueState = dueState;
  readonly formatPhone = formatPhone;

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly items = signal<CrmCustomerListItem[]>([]);
  readonly total = signal(0);
  readonly trips = signal<AdminTripListItem[]>([]);
  readonly admins = signal<CrmAdminUser[]>([]);

  readonly pageSize = 20;
  page = 1;
  search = '';
  status: CrmStatus | null = null;
  source: LeadSource | null = null;
  tripId: number | null = null;
  assignedAdminUserId: number | null = null;
  year: number | null = null;
  month: number | null = null;
  /** On by default: hides the thousands of plain website sign-ups with no enquiry, booking or contact. */
  activeOnly = true;

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.total() / this.pageSize));
  }

  get hasFilters(): boolean {
    return !!this.search || [this.status, this.source, this.tripId, this.assignedAdminUserId, this.year].some((v) => v !== null) || !this.activeOnly;
  }

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    this.search = q.get('search') ?? '';
    this.status = num(q.get('status')) as CrmStatus | null;
    this.source = num(q.get('source')) as LeadSource | null;
    this.tripId = num(q.get('tripId'));
    this.assignedAdminUserId = num(q.get('assigned'));
    this.year = num(q.get('year'));
    this.month = this.year ? num(q.get('month')) : null;
    this.activeOnly = q.get('all') !== '1';
    this.page = num(q.get('page')) ?? 1;

    this.tripService.getTrips(1, 100).subscribe({ next: (r) => this.trips.set(r.items), error: () => undefined });
    this.crm.admins().subscribe({ next: (a) => this.admins.set(a), error: () => undefined });
    this.searchChanges.pipe(debounceTime(300)).subscribe(() => this.apply());
    this.load();
  }

  ngOnDestroy(): void {
    this.searchChanges.complete();
  }

  onSearch(): void {
    this.searchChanges.next();
  }

  apply(): void {
    this.page = 1;
    this.syncUrlAndLoad();
  }

  clearFilters(): void {
    this.search = '';
    this.status = this.source = null;
    this.tripId = this.assignedAdminUserId = this.year = this.month = null;
    this.activeOnly = true;
    this.apply();
  }

  goToPage(page: number): void {
    this.page = Math.min(Math.max(1, page), this.totalPages);
    this.syncUrlAndLoad();
  }

  open(customer: CrmCustomerListItem): void {
    this.router.navigate(['/admin/crm/customers', customer.customerId]);
  }

  load(): void {
    this.state.set('loading');
    this.crm
      .customers({
        search: this.search.trim() || null,
        status: this.status,
        source: this.source,
        tripId: this.tripId,
        assignedAdminUserId: this.assignedAdminUserId,
        year: this.year,
        month: this.year ? this.month : null,
        activeOnly: this.activeOnly,
        page: this.page,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (r) => {
          this.items.set(r.items);
          this.total.set(r.totalCount);
          this.state.set('ready');
        },
        error: () => this.state.set('error'),
      });
  }

  private syncUrlAndLoad(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        search: this.search.trim() || null,
        status: this.status,
        source: this.source,
        tripId: this.tripId,
        assigned: this.assignedAdminUserId,
        year: this.year,
        month: this.year ? this.month : null,
        all: this.activeOnly ? null : 1,
        page: this.page > 1 ? this.page : null,
      },
    });
    this.load();
  }
}
