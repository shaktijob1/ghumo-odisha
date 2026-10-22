import { Component, EventEmitter, Input, OnChanges, Output, inject, signal, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { CrmEnquiryStatuses, CrmStatus, CrmStatusLabels, CrmStatuses } from '../../../../core/models/crm.model';
import { apiErrorMessage } from '../../../../shared/utils/api-error';

/**
 * Change the stage of a customer, or of one of their trip interests, with an optional reason (kept in
 * the history; for "Not interested" / "Cancelled" it's the lost reason). Booked and Completed aren't
 * offered for a trip interest — those follow a real booking.
 */
@Component({
  selector: 'app-crm-stage-dialog',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (open) {
      <div class="modal-backdrop" (click)="closed.emit()">
        <div class="modal" (click)="$event.stopPropagation()">
          <h4>Change stage{{ subject ? ' · ' + subject : '' }}</h4>
          <div class="fld">
            <span class="lbl">Stage</span>
            <select class="inp" [(ngModel)]="status">
              @for (s of options; track s) { <option [ngValue]="s">{{ labels[s] }}</option> }
            </select>
          </div>
          <div class="fld">
            <span class="lbl">{{ isLost ? 'Why was it lost?' : 'Reason (optional)' }}</span>
            <input class="inp" [(ngModel)]="reason" maxlength="500" [placeholder]="isLost ? 'Dates don\\'t suit, budget, chose another trip…' : 'Paid advance on UPI, asked for more time…'" />
          </div>
          @if (error()) { <div class="errorbox" style="margin-bottom:12px">{{ error() }}</div> }
          <div class="row" style="gap:8px">
            <button type="button" class="btn ghost" style="flex:1" (click)="closed.emit()">Cancel</button>
            <button type="button" class="btn" style="flex:1" [disabled]="saving() || status === current" (click)="save()">
              @if (saving()) { <span class="spin"></span> } @else { Save }
            </button>
          </div>
        </div>
      </div>
    }
  `,
})
export class CrmStageDialogComponent implements OnChanges {
  private readonly crm = inject(AdminCrmService);

  @Input() open = false;
  @Input() customerId: number | null = null;
  /** Set to change one trip interest's stage instead of the customer's. */
  @Input() interestId: number | null = null;
  @Input() current: CrmStatus = CrmStatus.NewLead;
  @Input() subject: string | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<CrmStatus>();

  readonly labels = CrmStatusLabels;
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  status: CrmStatus = CrmStatus.NewLead;
  reason = '';

  get options(): CrmStatus[] {
    return this.interestId ? CrmEnquiryStatuses : CrmStatuses;
  }

  get isLost(): boolean {
    return this.status === CrmStatus.NotInterested || this.status === CrmStatus.Cancelled;
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Reset only when the dialog opens — the page behind may pass new inputs while it is open.
    if (!changes['open']?.currentValue || changes['open'].previousValue) return;
    if (!this.open) return;
    this.status = this.current;
    this.reason = '';
    this.error.set(null);
  }

  save(): void {
    const reason = this.reason.trim() || null;
    const request$ = this.interestId
      ? this.crm.changeInterestStatus(this.interestId, this.status, reason)
      : this.crm.changeCustomerStatus(this.customerId!, this.status, reason);
    this.saving.set(true);
    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.emit(this.status);
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err, 'Could not change the stage.'));
      },
    });
  }
}
