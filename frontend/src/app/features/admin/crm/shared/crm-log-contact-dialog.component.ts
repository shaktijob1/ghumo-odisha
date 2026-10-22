import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { CrmChannel, CrmChannelLabels, CrmTripInterest } from '../../../../core/models/crm.model';
import { apiErrorMessage } from '../../../../shared/utils/api-error';

/** Quick picks for "what happened", per channel. */
const QUICK: Record<CrmChannel, string[]> = {
  [CrmChannel.Phone]: ['Called — spoke to them', 'Called — no answer', 'Called — asked to call back', 'They called us'],
  [CrmChannel.Email]: ['Email sent', 'Email received'],
  [CrmChannel.Other]: ['Met in person', 'Instagram message', 'Facebook message'],
  [CrmChannel.WhatsApp]: [],
};

/**
 * Record a call, email or other contact by hand. (WhatsApp messages are logged by the WhatsApp
 * button itself.) Updates the customer's "last contact".
 */
@Component({
  selector: 'app-crm-log-contact-dialog',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (open) {
      <div class="modal-backdrop" (click)="closed.emit()">
        <div class="modal" (click)="$event.stopPropagation()">
          <h4>Log a contact{{ customerName ? ' · ' + customerName : '' }}</h4>
          <div class="fld">
            <span class="lbl">How</span>
            <div class="row" style="gap:6px">
              @for (c of channels; track c) {
                <button type="button" class="btn sm" [class.ghost]="channel !== c" (click)="setChannel(c)">{{ labels[c] }}</button>
              }
            </div>
          </div>
          <div class="fld">
            <span class="lbl">What happened</span>
            <input class="inp" [(ngModel)]="action" maxlength="200" placeholder="Called — spoke to them" />
            <div class="row wrap crm-quick">
              @for (q of quick[channel]; track q) { <button type="button" class="btn sm ghost" (click)="action = q">{{ q }}</button> }
            </div>
          </div>
          @if (interests.length > 0) {
            <div class="fld">
              <span class="lbl">About</span>
              <select class="inp" [(ngModel)]="interestId">
                <option [ngValue]="null">General</option>
                @for (i of interests; track i.customerTripInterestId) { <option [ngValue]="i.customerTripInterestId">{{ i.tripTitle }}</option> }
              </select>
            </div>
          }
          <div class="fld">
            <span class="lbl">Details (optional)</span>
            <textarea class="inp" rows="3" [(ngModel)]="details" maxlength="4000" placeholder="Wants 3 seats, will pay the advance on Friday"></textarea>
          </div>
          @if (error()) { <div class="errorbox" style="margin-bottom:12px">{{ error() }}</div> }
          <div class="row" style="gap:8px">
            <button type="button" class="btn ghost" style="flex:1" (click)="closed.emit()">Cancel</button>
            <button type="button" class="btn" style="flex:1" [disabled]="saving() || !action.trim()" (click)="save()">
              @if (saving()) { <span class="spin"></span> } @else { Save }
            </button>
          </div>
        </div>
      </div>
    }
  `,
  styleUrls: ['../crm-shared.css'],
})
export class CrmLogContactDialogComponent implements OnChanges {
  private readonly crm = inject(AdminCrmService);

  @Input() open = false;
  @Input() customerId: number | null = null;
  @Input() customerName: string | null = null;
  @Input() interests: CrmTripInterest[] = [];
  @Input() interestIdPreset: number | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  readonly channels = [CrmChannel.Phone, CrmChannel.Email, CrmChannel.Other];
  readonly labels = CrmChannelLabels;
  readonly quick = QUICK;
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  channel = CrmChannel.Phone;
  action = '';
  details = '';
  interestId: number | null = null;

  ngOnChanges(changes: SimpleChanges): void {
    // Reset only when the dialog opens — the page behind may pass new inputs while it is open.
    if (!changes['open']?.currentValue || changes['open'].previousValue) return;
    this.channel = CrmChannel.Phone;
    this.action = '';
    this.details = '';
    this.interestId = this.interestIdPreset;
    this.error.set(null);
  }

  setChannel(channel: CrmChannel): void {
    this.channel = channel;
    this.action = '';
  }

  save(): void {
    if (!this.customerId || !this.action.trim()) return;
    this.saving.set(true);
    this.crm.logContact(this.customerId, this.interestId, this.channel, this.action.trim(), this.details.trim() || null).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.emit();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err, 'Could not save.'));
      },
    });
  }
}
