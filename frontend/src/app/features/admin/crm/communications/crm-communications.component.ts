import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subject, debounceTime } from 'rxjs';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { AdminTripService } from '../../../../core/services/admin-trip.service';
import { ToastService } from '../../../../core/services/toast.service';
import { AdminTripListItem } from '../../../../core/models/trip.model';
import {
  CrmAdminUser,
  CrmChannel,
  CrmChannelLabels,
  CrmCommunication,
  CrmMessageStatus,
  CrmMessageStatusLabels,
} from '../../../../core/models/crm.model';
import { StatePanelComponent } from '../../../../shared/components/state-panel.component';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { formatPhone } from '../crm-utils';

function num(value: string | null): number | null {
  if (value === null || value === '') return null;
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

/** Badge colour per message state. "Opened WhatsApp" is amber: the admin hasn't said it was sent. */
const STATUS_TONE: Record<CrmMessageStatus, string> = {
  [CrmMessageStatus.WhatsAppOpened]: 'wait',
  [CrmMessageStatus.SentManually]: 'ok',
  [CrmMessageStatus.NotConfirmed]: '',
  [CrmMessageStatus.Logged]: 'info',
};

/**
 * Admin → CRM → Communication history: every WhatsApp the admin opened (and whether they confirmed
 * sending it) and every call / email / other contact they logged. No delivery or read states — the
 * CRM can't know those without the WhatsApp API.
 */
@Component({
  selector: 'app-crm-communications',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent],
  templateUrl: './crm-communications.component.html',
  styleUrls: ['../crm-shared.css', './crm-communications.component.css'],
})
export class CrmCommunicationsComponent implements OnInit, OnDestroy {
  private readonly crm = inject(AdminCrmService);
  private readonly tripService = inject(AdminTripService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly searchChanges = new Subject<void>();

  readonly channelLabels = CrmChannelLabels;
  readonly statusLabels = CrmMessageStatusLabels;
  readonly statusTone = STATUS_TONE;
  readonly channels = [CrmChannel.WhatsApp, CrmChannel.Phone, CrmChannel.Email, CrmChannel.Other];
  readonly statuses = [CrmMessageStatus.WhatsAppOpened, CrmMessageStatus.SentManually, CrmMessageStatus.NotConfirmed, CrmMessageStatus.Logged];
  readonly MessageStatus = CrmMessageStatus;
  readonly Channel = CrmChannel;
  readonly formatPhone = formatPhone;

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly items = signal<CrmCommunication[]>([]);
  readonly total = signal(0);
  readonly admins = signal<CrmAdminUser[]>([]);
  readonly trips = signal<AdminTripListItem[]>([]);
  readonly expanded = signal<number | null>(null);
  readonly busy = signal<number | null>(null);

  readonly pageSize = 50;
  page = 1;
  search = '';
  channel: CrmChannel | null = null;
  status: CrmMessageStatus | null = null;
  adminUserId: number | null = null;
  tripId: number | null = null;
  from = '';
  to = '';

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.total() / this.pageSize));
  }

  get hasFilters(): boolean {
    return !!this.search || !!this.from || !!this.to || [this.channel, this.status, this.adminUserId, this.tripId].some((v) => v !== null);
  }

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    this.search = q.get('search') ?? '';
    this.channel = num(q.get('channel')) as CrmChannel | null;
    this.status = num(q.get('status')) as CrmMessageStatus | null;
    this.adminUserId = num(q.get('admin'));
    this.tripId = num(q.get('tripId'));
    this.from = q.get('from') ?? '';
    this.to = q.get('to') ?? '';
    this.crm.admins().subscribe({ next: (a) => this.admins.set(a), error: () => undefined });
    this.tripService.getTrips(1, 100).subscribe({ next: (r) => this.trips.set(r.items), error: () => undefined });
    this.searchChanges.pipe(debounceTime(300)).subscribe(() => this.apply());
    this.load();
  }

  ngOnDestroy(): void {
    this.searchChanges.complete();
  }

  onSearch(): void {
    this.searchChanges.next();
  }

  /** Quick filter: WhatsApp messages still waiting for "Mark as sent". */
  showUnconfirmed(): void {
    this.channel = CrmChannel.WhatsApp;
    this.status = CrmMessageStatus.WhatsAppOpened;
    this.apply();
  }

  clearFilters(): void {
    this.search = this.from = this.to = '';
    this.channel = this.status = null;
    this.adminUserId = this.tripId = null;
    this.apply();
  }

  apply(): void {
    this.page = 1;
    this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        search: this.search.trim() || null,
        channel: this.channel,
        status: this.status,
        admin: this.adminUserId,
        tripId: this.tripId,
        from: this.from || null,
        to: this.to || null,
      },
    });
    this.load();
  }

  goToPage(page: number): void {
    this.page = Math.min(Math.max(1, page), this.totalPages);
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.crm
      .communications({
        search: this.search.trim() || null,
        channel: this.channel,
        status: this.status,
        adminUserId: this.adminUserId,
        tripId: this.tripId,
        from: this.from || null,
        to: this.to || null,
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

  toggle(c: CrmCommunication): void {
    this.expanded.set(this.expanded() === c.crmCommunicationId ? null : c.crmCommunicationId);
  }

  /** The admin confirms (or denies) pressing Send for a WhatsApp message opened earlier. */
  confirm(c: CrmCommunication, sent: boolean): void {
    this.busy.set(c.crmCommunicationId);
    (sent ? this.crm.markSent(c.crmCommunicationId) : this.crm.markNotSent(c.crmCommunicationId)).subscribe({
      next: () => {
        this.busy.set(null);
        this.toast.success(sent ? 'Marked as sent.' : 'Marked as not sent.');
        this.load();
      },
      error: (err) => {
        this.busy.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not update the message.'));
      },
    });
  }
}
