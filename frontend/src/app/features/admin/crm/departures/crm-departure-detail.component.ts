import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { ToastService } from '../../../../core/services/toast.service';
import { BookingStatus } from '../../../../core/models/enums.model';
import {
  CrmCustomerListItem,
  CrmDepartureDetail,
  CrmDepartureStatus,
  CrmDepartureStatusLabels,
  CrmDepartureStatusTone,
  CrmStatus,
  CrmStatusLabels,
  CrmStatusTone,
  CrmTripInterest,
  LeadSourceLabels,
} from '../../../../core/models/crm.model';
import { StatePanelComponent } from '../../../../shared/components/state-panel.component';
import { CrmCustomerPickerComponent } from '../shared/crm-customer-picker.component';
import { CrmInterestDialogComponent } from '../shared/crm-interest-dialog.component';
import { CrmInterestEditDialogComponent } from '../shared/crm-interest-edit-dialog.component';
import { CrmNoteDialogComponent } from '../shared/crm-note-dialog.component';
import { CrmStageDialogComponent } from '../shared/crm-stage-dialog.component';
import { CrmDepartureDialogComponent } from './crm-departure-dialog.component';
import { CrmFollowUpAddDialogComponent } from '../shared/crm-followup-dialogs.component';
import { CrmWhatsAppDialogComponent } from '../shared/crm-whatsapp-dialog.component';
import { dueState, formatPhone } from '../crm-utils';

/** Admin → CRM → one departure: seats against demand, its customers and its real bookings. */
@Component({
  selector: 'app-crm-departure-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    StatePanelComponent,
    CrmDepartureDialogComponent,
    CrmCustomerPickerComponent,
    CrmInterestDialogComponent,
    CrmInterestEditDialogComponent,
    CrmNoteDialogComponent,
    CrmStageDialogComponent,
    CrmFollowUpAddDialogComponent,
    CrmWhatsAppDialogComponent,
  ],
  templateUrl: './crm-departure-detail.component.html',
  styleUrls: ['../crm-shared.css', './crm-departures.css', './crm-departure-detail.component.css'],
})
export class CrmDepartureDetailComponent implements OnInit {
  private readonly crm = inject(AdminCrmService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly statusLabels = CrmStatusLabels;
  readonly statusTone = CrmStatusTone;
  readonly departureLabels = CrmDepartureStatusLabels;
  readonly departureTone = CrmDepartureStatusTone;
  readonly sourceLabels = LeadSourceLabels;
  readonly CrmDepartureStatus = CrmDepartureStatus;
  readonly BookingStatus = BookingStatus;
  readonly dueState = dueState;
  readonly formatPhone = formatPhone;

  readonly state = signal<'loading' | 'ready' | 'error' | 'not-found'>('loading');
  readonly data = signal<CrmDepartureDetail | null>(null);
  readonly stageFilter = signal<CrmStatus | null>(null);

  // Dialogs
  readonly editDepartureOpen = signal(false);
  readonly pickerOpen = signal(false);
  readonly addFor = signal<CrmCustomerListItem | null>(null);
  readonly editInterest = signal<CrmTripInterest | null>(null);
  readonly noteFor = signal<CrmTripInterest | null>(null);
  readonly stageFor = signal<CrmTripInterest | null>(null);
  readonly followUpFor = signal<CrmTripInterest | null>(null);
  readonly waFor = signal<CrmTripInterest | null>(null);

  readonly stageCounts = computed(() => {
    const counts = new Map<CrmStatus, number>();
    for (const i of this.data()?.interests ?? []) counts.set(i.status, (counts.get(i.status) ?? 0) + 1);
    return [...counts.entries()].sort((a, b) => a[0] - b[0]);
  });

  readonly visibleInterests = computed(() => {
    const filter = this.stageFilter();
    const all = this.data()?.interests ?? [];
    return filter === null ? all : all.filter((i) => i.status === filter);
  });

  get slotId(): number {
    return Number(this.route.snapshot.paramMap.get('id'));
  }

  ngOnInit(): void {
    this.load(true);
  }

  load(first = false): void {
    if (first) this.state.set('loading');
    this.crm.departure(this.slotId).subscribe({
      next: (d) => {
        this.data.set(d);
        this.state.set('ready');
      },
      error: (err) => this.state.set(err?.status === 404 ? 'not-found' : 'error'),
    });
  }

  seatShare(): number {
    const d = this.data()?.departure;
    return d && d.totalSeats ? Math.min(100, Math.round((d.bookedSeats * 100) / d.totalSeats)) : 0;
  }

  minimumMark(): number | null {
    const d = this.data()?.departure;
    return d?.minimumBookedSeats && d.totalSeats ? Math.round((d.minimumBookedSeats * 100) / d.totalSeats) : null;
  }

  onPicked(customer: CrmCustomerListItem): void {
    this.pickerOpen.set(false);
    this.addFor.set(customer);
  }

  afterSave(message: string): void {
    this.editDepartureOpen.set(false);
    this.addFor.set(null);
    this.editInterest.set(null);
    this.noteFor.set(null);
    this.stageFor.set(null);
    this.followUpFor.set(null);
    this.toast.success(message);
    this.load();
  }

  open(i: CrmTripInterest): void {
    this.router.navigate(['/admin/crm/customers', i.customerId]);
  }

  bookingParams(i: CrmTripInterest): Record<string, number> {
    return { customerId: i.customerId, tripId: i.tripId, slotId: this.slotId, seats: i.travellers };
  }

  canConvert(i: CrmTripInterest): boolean {
    return !i.bookingId && i.status !== CrmStatus.NotInterested && i.status !== CrmStatus.Cancelled;
  }
}
