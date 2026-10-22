import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminAuthService } from '../../../../core/services/admin-auth.service';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { AdminTripService } from '../../../../core/services/admin-trip.service';
import { ToastService } from '../../../../core/services/toast.service';
import { AdminTripListItem } from '../../../../core/models/trip.model';
import {
  CrmAdminUser,
  CrmFollowUp,
  CrmFollowUpBucket,
  CrmFollowUpSummary,
  CrmStatus,
  CrmStatusLabels,
  CrmStatusTone,
  CrmTemplateCategory,
} from '../../../../core/models/crm.model';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog.component';
import { StatePanelComponent } from '../../../../shared/components/state-panel.component';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { CrmFollowUpCompleteDialogComponent, CrmFollowUpRescheduleDialogComponent } from '../shared/crm-followup-dialogs.component';
import { CrmNoteDialogComponent } from '../shared/crm-note-dialog.component';
import { CrmStageDialogComponent } from '../shared/crm-stage-dialog.component';
import { CrmWhatsAppDialogComponent } from '../shared/crm-whatsapp-dialog.component';
import { CrmLogContactDialogComponent } from '../shared/crm-log-contact-dialog.component';
import { formatPhone } from '../crm-utils';
import { toLocalDateKey } from '../../../../shared/utils/date-key';

const BUCKETS: { bucket: CrmFollowUpBucket; key: string; label: string }[] = [
  { bucket: CrmFollowUpBucket.Today, key: 'today', label: 'Today' },
  { bucket: CrmFollowUpBucket.Overdue, key: 'overdue', label: 'Overdue' },
  { bucket: CrmFollowUpBucket.Upcoming, key: 'upcoming', label: 'Upcoming' },
  { bucket: CrmFollowUpBucket.Pending, key: 'all', label: 'All pending' },
  { bucket: CrmFollowUpBucket.Done, key: 'done', label: 'Done' },
];

/** Admin → CRM → Follow-ups: who to call today, who was missed, and who's coming up. */
@Component({
  selector: 'app-crm-followups',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    StatePanelComponent,
    ConfirmDialogComponent,
    CrmFollowUpCompleteDialogComponent,
    CrmFollowUpRescheduleDialogComponent,
    CrmNoteDialogComponent,
    CrmStageDialogComponent,
    CrmWhatsAppDialogComponent,
    CrmLogContactDialogComponent,
  ],
  templateUrl: './crm-followups.component.html',
  styleUrls: ['../crm-shared.css', './crm-followups.component.css'],
})
export class CrmFollowUpsComponent implements OnInit {
  private readonly crm = inject(AdminCrmService);
  private readonly auth = inject(AdminAuthService);
  private readonly tripService = inject(AdminTripService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly buckets = BUCKETS;
  readonly Bucket = CrmFollowUpBucket;
  readonly CrmStatus = CrmStatus;
  readonly statusLabels = CrmStatusLabels;
  readonly statusTone = CrmStatusTone;
  readonly formatPhone = formatPhone;

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly items = signal<CrmFollowUp[]>([]);
  readonly total = signal(0);
  readonly summary = signal<CrmFollowUpSummary | null>(null);
  readonly admins = signal<CrmAdminUser[]>([]);
  readonly trips = signal<AdminTripListItem[]>([]);

  // Dialogs
  readonly completeFor = signal<CrmFollowUp | null>(null);
  readonly rescheduleFor = signal<CrmFollowUp | null>(null);
  readonly noteFor = signal<CrmFollowUp | null>(null);
  readonly stageFor = signal<CrmFollowUp | null>(null);
  readonly cancelFor = signal<CrmFollowUp | null>(null);
  readonly waFor = signal<CrmFollowUp | null>(null);
  readonly logFor = signal<CrmFollowUp | null>(null);
  readonly FollowUpCategory = CrmTemplateCategory.FollowUp;

  readonly pageSize = 50;
  page = 1;
  bucket = CrmFollowUpBucket.Today;
  assignedAdminUserId: number | null = null;
  tripId: number | null = null;

  /** Upcoming / all-pending lists are grouped under a heading per day. */
  readonly groups = computed(() => {
    const groups: { date: string; items: CrmFollowUp[] }[] = [];
    for (const f of this.items()) {
      // Done: the India calendar day it was completed (completedAt is a UTC timestamp).
      const date = this.bucket === CrmFollowUpBucket.Done && f.completedAt ? toLocalDateKey(new Date(f.completedAt)) : f.dueDate;
      const last = groups[groups.length - 1];
      if (last && last.date === date) last.items.push(f);
      else groups.push({ date, items: [f] });
    }
    return groups;
  });

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.total() / this.pageSize));
  }

  get myId(): number | null {
    return this.auth.currentAdmin()?.adminUserId ?? null;
  }

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    this.bucket = BUCKETS.find((b) => b.key === q.get('list'))?.bucket ?? CrmFollowUpBucket.Today;
    this.assignedAdminUserId = Number(q.get('assigned')) || null;
    this.tripId = Number(q.get('tripId')) || null;
    this.crm.admins().subscribe({ next: (a) => this.admins.set(a), error: () => undefined });
    this.tripService.getTrips(1, 100).subscribe({ next: (r) => this.trips.set(r.items), error: () => undefined });
    this.reload();
  }

  setBucket(bucket: CrmFollowUpBucket): void {
    this.bucket = bucket;
    this.apply();
  }

  apply(): void {
    this.page = 1;
    this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: {
        list: BUCKETS.find((b) => b.bucket === this.bucket)?.key,
        assigned: this.assignedAdminUserId,
        tripId: this.tripId,
      },
    });
    this.load();
  }

  goToPage(page: number): void {
    this.page = Math.min(Math.max(1, page), this.totalPages);
    this.load();
  }

  reload(): void {
    this.crm.followUpSummary().subscribe({ next: (s) => this.summary.set(s), error: () => undefined });
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.crm
      .followUps({ bucket: this.bucket, assignedAdminUserId: this.assignedAdminUserId, tripId: this.tripId, page: this.page, pageSize: this.pageSize })
      .subscribe({
        next: (r) => {
          this.items.set(r.items);
          this.total.set(r.totalCount);
          this.state.set('ready');
        },
        error: () => this.state.set('error'),
      });
  }

  count(bucket: CrmFollowUpBucket): number | null {
    const s = this.summary();
    if (!s) return null;
    return bucket === CrmFollowUpBucket.Today ? s.today : bucket === CrmFollowUpBucket.Overdue ? s.overdue : bucket === CrmFollowUpBucket.Upcoming ? s.upcoming : null;
  }

  stageOf(f: CrmFollowUp): CrmStatus {
    return f.interestStatus ?? f.customerStatus;
  }

  /** "Follow-up today", "Due 2 days ago", "Due tomorrow", "Due Thu, 9 Oct". */
  dueText(f: CrmFollowUp): string {
    if (f.daysOverdue > 0) return f.daysOverdue === 1 ? 'Was due yesterday' : `Was due ${f.daysOverdue} days ago`;
    const today = new Date();
    const due = new Date(f.dueDate + 'T00:00:00');
    const days = Math.round((due.getTime() - new Date(today.getFullYear(), today.getMonth(), today.getDate()).getTime()) / 86400000);
    if (days === 0) return 'Follow-up today';
    if (days === 1) return 'Due tomorrow';
    return 'Due ' + due.toLocaleDateString('en-IN', { weekday: 'short', day: 'numeric', month: 'short' });
  }

  afterSave(message: string): void {
    this.completeFor.set(null);
    this.rescheduleFor.set(null);
    this.noteFor.set(null);
    this.stageFor.set(null);
    this.logFor.set(null);
    this.toast.success(message);
    this.reload();
  }

  confirmCancel(): void {
    const f = this.cancelFor();
    if (!f) return;
    this.crm.cancelFollowUp(f.crmFollowUpId).subscribe({
      next: () => {
        this.cancelFor.set(null);
        this.afterSave('Follow-up cancelled.');
      },
      error: (err) => {
        this.cancelFor.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not cancel the follow-up.'));
      },
    });
  }
}
