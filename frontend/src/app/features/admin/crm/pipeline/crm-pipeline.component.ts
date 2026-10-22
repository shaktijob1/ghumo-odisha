import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { AdminTripService } from '../../../../core/services/admin-trip.service';
import { ToastService } from '../../../../core/services/toast.service';
import { AdminTripListItem } from '../../../../core/models/trip.model';
import {
  CrmDeparture,
  CrmEnquiryStatuses,
  CrmPipelineColumn,
  CrmStatus,
  CrmStatusLabels,
  CrmStatusTone,
  CrmTripInterest,
  LeadSource,
  LeadSourceLabels,
  LeadSources,
} from '../../../../core/models/crm.model';
import { StatePanelComponent } from '../../../../shared/components/state-panel.component';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { dueState } from '../crm-utils';
import { CrmWhatsAppDialogComponent } from '../shared/crm-whatsapp-dialog.component';

function num(value: string | null): number | null {
  if (value === null || value === '') return null;
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

/**
 * Sales pipeline (Kanban): New lead → Interested → Hot lead → Booking pending → Booked → Completed.
 * Cards are trip interests. Dragging a card (or "Move to…" on touch screens) changes its stage on the
 * server, which keeps the history. Booked / Completed only come from a real booking.
 */
@Component({
  selector: 'app-crm-pipeline',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent, CrmWhatsAppDialogComponent],
  templateUrl: './crm-pipeline.component.html',
  styleUrls: ['../crm-shared.css', './crm-pipeline.component.css'],
})
export class CrmPipelineComponent implements OnInit {
  private readonly crm = inject(AdminCrmService);
  private readonly tripService = inject(AdminTripService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly statusLabels = CrmStatusLabels;
  readonly statusTone = CrmStatusTone;
  readonly moveTargets = CrmEnquiryStatuses;
  readonly sourceLabels = LeadSourceLabels;
  readonly sources = LeadSources;
  readonly dueState = dueState;

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly columns = signal<CrmPipelineColumn[]>([]);
  readonly trips = signal<AdminTripListItem[]>([]);
  readonly departures = signal<CrmDeparture[]>([]);
  readonly dragging = signal<CrmTripInterest | null>(null);
  readonly dropTarget = signal<CrmStatus | null>(null);
  readonly moving = signal<number | null>(null);
  readonly waFor = signal<CrmTripInterest | null>(null);

  tripId: number | null = null;
  tripDateSlotId: number | null = null;
  source: LeadSource | null = null;

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    this.tripId = num(q.get('tripId'));
    this.tripDateSlotId = this.tripId ? num(q.get('tripDateSlotId')) : null;
    this.source = num(q.get('source')) as LeadSource | null;
    this.tripService.getTrips(1, 100).subscribe({ next: (r) => this.trips.set(r.items), error: () => undefined });
    if (this.tripId) this.loadDepartures();
    this.load();
  }

  onTripChange(): void {
    this.tripDateSlotId = null;
    this.departures.set([]);
    if (this.tripId) this.loadDepartures();
    this.apply();
  }

  apply(): void {
    this.router.navigate([], {
      relativeTo: this.route,
      replaceUrl: true,
      queryParams: { tripId: this.tripId, tripDateSlotId: this.tripId ? this.tripDateSlotId : null, source: this.source },
    });
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.crm.pipeline(this.tripId, this.tripDateSlotId, this.source, 40).subscribe({
      next: (p) => {
        this.columns.set(p.columns);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  isBookingStage(status: CrmStatus): boolean {
    return status === CrmStatus.Booked || status === CrmStatus.Completed;
  }

  // ---------- Drag and drop ----------

  onDragStart(event: DragEvent, card: CrmTripInterest): void {
    this.dragging.set(card);
    event.dataTransfer?.setData('text/plain', String(card.customerTripInterestId));
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  onDragEnd(): void {
    this.dragging.set(null);
    this.dropTarget.set(null);
  }

  onDragOver(event: DragEvent, status: CrmStatus): void {
    const card = this.dragging();
    if (!card || card.status === status) return;
    event.preventDefault();
    this.dropTarget.set(status);
  }

  onDrop(event: DragEvent, status: CrmStatus): void {
    event.preventDefault();
    const card = this.dragging();
    this.onDragEnd();
    if (card) this.move(card, status);
  }

  /** Change a card's stage. Booked / Completed can't be set by hand — they follow the real booking. */
  move(card: CrmTripInterest, status: CrmStatus): void {
    if (card.status === status) return;
    if (this.isBookingStage(status)) {
      this.toast.error(card.bookingId
        ? 'This lead already has a booking — its stage follows the booking.'
        : 'Use “Convert to booking” on the card — it moves to Booked once the booking is confirmed.');
      return;
    }

    this.moving.set(card.customerTripInterestId);
    this.crm.changeInterestStatus(card.customerTripInterestId, status, null).subscribe({
      next: () => {
        this.moving.set(null);
        this.toast.success(`${card.customerName || 'Lead'} moved to ${this.statusLabels[status]}.`);
        this.load();
      },
      error: (err) => {
        this.moving.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not move this lead.'));
      },
    });
  }

  onMoveSelect(card: CrmTripInterest, value: string): void {
    const status = Number(value) as CrmStatus;
    if (value !== '' && Number.isFinite(status)) this.move(card, status);
  }

  bookingParams(card: CrmTripInterest): Record<string, number> {
    const params: Record<string, number> = { customerId: card.customerId, tripId: card.tripId, seats: card.travellers };
    if (card.tripDateSlotId) params['slotId'] = card.tripDateSlotId;
    return params;
  }

  private loadDepartures(): void {
    this.crm.departures({ tripId: this.tripId, includePast: true, pageSize: 100 }).subscribe({
      next: (r) => this.departures.set(r.items),
      error: () => this.departures.set([]),
    });
  }
}
