import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { ToastService } from '../../../../core/services/toast.service';
import { BookingStatus } from '../../../../core/models/enums.model';
import {
  CrmAdminUser,
  CrmChannelLabels,
  CrmCustomerDetail,
  CrmFollowUp,
  CrmFollowUpStatus,
  CrmMessageStatus,
  CrmMessageStatusLabels,
  CrmStatus,
  CrmStatusLabels,
  CrmStatusTone,
  CrmTripInterest,
  LeadSourceLabels,
} from '../../../../core/models/crm.model';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog.component';
import { StatePanelComponent } from '../../../../shared/components/state-panel.component';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { CrmInterestDialogComponent } from '../shared/crm-interest-dialog.component';
import { CrmStageDialogComponent } from '../shared/crm-stage-dialog.component';
import { CrmWhatsAppDialogComponent } from '../shared/crm-whatsapp-dialog.component';
import { CrmLogContactDialogComponent } from '../shared/crm-log-contact-dialog.component';
import { CrmFollowUpAddDialogComponent, CrmFollowUpCompleteDialogComponent, CrmFollowUpRescheduleDialogComponent } from '../shared/crm-followup-dialogs.component';
import { dueState, formatPhone } from '../crm-utils';

/** One line in the profile's activity timeline: a contact or a stage change. */
interface ActivityItem {
  at: string;
  kind: 'contact' | 'stage';
  title: string;
  detail: string | null;
  by: string | null;
  /** A WhatsApp message still waiting for "Mark as sent". */
  unconfirmedId?: number;
}

const BOOKING_STATUS_LABELS: Record<BookingStatus, string> = {
  [BookingStatus.AwaitingPayment]: 'Unpaid',
  [BookingStatus.Pending]: 'Unpaid',
  [BookingStatus.Confirmed]: 'Confirmed',
  [BookingStatus.Rejected]: 'Rejected',
  [BookingStatus.Cancelled]: 'Cancelled',
  [BookingStatus.Completed]: 'Completed',
};

@Component({
  selector: 'app-crm-customer-detail',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    StatePanelComponent,
    ConfirmDialogComponent,
    CrmInterestDialogComponent,
    CrmStageDialogComponent,
    CrmFollowUpAddDialogComponent,
    CrmFollowUpCompleteDialogComponent,
    CrmFollowUpRescheduleDialogComponent,
    CrmWhatsAppDialogComponent,
    CrmLogContactDialogComponent,
  ],
  templateUrl: './crm-customer-detail.component.html',
  styleUrls: ['../crm-shared.css', './crm-customer-detail.component.css'],
})
export class CrmCustomerDetailComponent implements OnInit {
  private readonly crm = inject(AdminCrmService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);

  readonly statusLabels = CrmStatusLabels;
  readonly statusTone = CrmStatusTone;
  readonly sourceLabels = LeadSourceLabels;
  readonly channelLabels = CrmChannelLabels;
  readonly messageLabels = CrmMessageStatusLabels;
  readonly bookingLabels = BOOKING_STATUS_LABELS;
  readonly BookingStatus = BookingStatus;
  readonly CrmFollowUpStatus = CrmFollowUpStatus;
  readonly dueState = dueState;
  readonly formatPhone = formatPhone;

  readonly state = signal<'loading' | 'ready' | 'error' | 'not-found'>('loading');
  readonly customer = signal<CrmCustomerDetail | null>(null);

  // Dialogs
  readonly interestDialogOpen = signal(false);
  readonly stageDialog = signal<{ interest: CrmTripInterest | null } | null>(null);
  readonly deleteTarget = signal<CrmTripInterest | null>(null);
  readonly addFollowUpOpen = signal(false);
  /** WhatsApp dialog: open for the whole customer (interestId null) or one trip interest. */
  readonly waFor = signal<{ interestId: number | null } | null>(null);
  readonly logOpen = signal(false);
  readonly confirming = signal<number | null>(null);
  readonly completeFor = signal<CrmFollowUp | null>(null);
  readonly rescheduleFor = signal<CrmFollowUp | null>(null);
  readonly admins = signal<CrmAdminUser[]>([]);

  noteText = '';
  noteInterestId: number | null = null;
  readonly savingNote = signal(false);

  readonly pendingFollowUps = computed(() => (this.customer()?.followUps ?? []).filter((f) => f.status === CrmFollowUpStatus.Pending));
  readonly doneFollowUps = computed(() => (this.customer()?.followUps ?? []).filter((f) => f.status === CrmFollowUpStatus.Done).slice(0, 5));

  /** Contacts and stage changes, newest first. */
  readonly activity = computed<ActivityItem[]>(() => {
    const c = this.customer();
    if (!c) return [];
    const contacts: ActivityItem[] = c.communications.map((m) => ({
      at: m.createdAt,
      kind: 'contact',
      title: `${this.channelLabels[m.channel]} · ${m.action}`,
      detail: [m.templateName, m.tripTitle, this.messageLabels[m.status]].filter(Boolean).join(' · ') || null,
      by: m.adminName,
      unconfirmedId: m.status === CrmMessageStatus.WhatsAppOpened ? m.crmCommunicationId : undefined,
    }));
    const stages: ActivityItem[] = c.statusHistory.map((s) => ({
      at: s.createdAt,
      kind: 'stage',
      title: s.fromStatus === null ? `${s.tripTitle ? s.tripTitle + ': ' : ''}${this.statusLabels[s.toStatus]}` : `${s.tripTitle ? s.tripTitle + ': ' : ''}${this.statusLabels[s.fromStatus]} → ${this.statusLabels[s.toStatus]}`,
      detail: s.reason,
      by: s.adminName ?? (s.reason ? null : 'Automatic'),
    }));
    return [...contacts, ...stages].sort((a, b) => b.at.localeCompare(a.at)).slice(0, 40);
  });

  get customerId(): number {
    return Number(this.route.snapshot.paramMap.get('id'));
  }

  ngOnInit(): void {
    this.load(true);
    this.crm.admins().subscribe({ next: (a) => this.admins.set(a), error: () => undefined });
  }

  /** Confirm a WhatsApp message opened earlier was actually sent (or not). */
  confirmSent(communicationId: number, sent: boolean): void {
    this.confirming.set(communicationId);
    (sent ? this.crm.markSent(communicationId) : this.crm.markNotSent(communicationId)).subscribe({
      next: () => {
        this.confirming.set(null);
        this.toast.success(sent ? 'Marked as sent.' : 'Marked as not sent.');
        this.load();
      },
      error: (err) => {
        this.confirming.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not update the message.'));
      },
    });
  }

  onFollowUpSaved(message: string): void {
    this.addFollowUpOpen.set(false);
    this.logOpen.set(false);
    this.completeFor.set(null);
    this.rescheduleFor.set(null);
    this.toast.success(message);
    this.load();
  }

  load(first = false): void {
    if (first) this.state.set('loading');
    this.crm.customer(this.customerId).subscribe({
      next: (c) => {
        this.customer.set(c);
        this.state.set('ready');
        // Arrived from the duplicate warning on "Add customer": open "Add trip interest" straight away.
        if (first && this.route.snapshot.queryParamMap.get('addInterest') === '1') this.interestDialogOpen.set(true);
      },
      error: (err) => this.state.set(err?.status === 404 ? 'not-found' : 'error'),
    });
  }

  onInterestSaved(): void {
    this.interestDialogOpen.set(false);
    this.toast.success('Trip interest added.');
    this.load();
  }

  onStageSaved(): void {
    this.stageDialog.set(null);
    this.toast.success('Stage updated.');
    this.load();
  }

  addNote(): void {
    const text = this.noteText.trim();
    if (!text) return;
    this.savingNote.set(true);
    this.crm.addNote(this.customerId, text, this.noteInterestId).subscribe({
      next: () => {
        this.savingNote.set(false);
        this.noteText = '';
        this.noteInterestId = null;
        this.load();
      },
      error: (err) => {
        this.savingNote.set(false);
        this.toast.error(apiErrorMessage(err, 'Could not save the note.'));
      },
    });
  }

  confirmDelete(): void {
    const target = this.deleteTarget();
    if (!target) return;
    this.crm.deleteInterest(target.customerTripInterestId).subscribe({
      next: () => {
        this.deleteTarget.set(null);
        this.toast.success('Trip interest removed.');
        this.load();
      },
      error: (err) => {
        this.deleteTarget.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not remove the trip interest.'));
      },
    });
  }

  /** Query string for the admin booking form, pre-filled from a trip interest (or just the customer). */
  bookingParams(interest: CrmTripInterest | null): Record<string, number> {
    const params: Record<string, number> = { customerId: this.customerId };
    if (interest) {
      params['tripId'] = interest.tripId;
      params['seats'] = interest.travellers;
      if (interest.tripDateSlotId) params['slotId'] = interest.tripDateSlotId;
    }
    return params;
  }

  isOpenInterest(i: CrmTripInterest): boolean {
    return !i.bookingId && i.status !== CrmStatus.NotInterested && i.status !== CrmStatus.Cancelled && i.status !== CrmStatus.Completed;
  }

  interestTitle(id: number | null): string | null {
    if (!id) return null;
    return this.customer()?.interests.find((i) => i.customerTripInterestId === id)?.tripTitle ?? null;
  }
}
