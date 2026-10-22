import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import {
  CrmRenderedMessage,
  CrmTemplate,
  CrmTemplateCategory,
  CrmTemplateCategoryLabels,
  CrmTripInterest,
} from '../../../../core/models/crm.model';
import { apiErrorMessage } from '../../../../shared/utils/api-error';

/**
 * Manual WhatsApp: pick a template → the server fills it in for this customer (and trip) → the admin
 * reviews/edits it → WhatsApp Web (or the app) opens with the number and text ready, and the admin
 * presses Send there. Nothing is sent automatically. Afterwards the admin can confirm "Mark as sent";
 * until then the log only says "Opened WhatsApp" — never delivered or read.
 */
@Component({
  selector: 'app-crm-whatsapp-dialog',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (open) {
      <div class="modal-backdrop" (click)="close()">
        <div class="modal crm-modal crm-wa" (click)="$event.stopPropagation()">
          @if (step() === 'compose') {
            <h4>WhatsApp · {{ customerName || 'Customer' }}</h4>

            <div class="f2">
              <div class="fld">
                <span class="lbl">Template</span>
                <select class="inp" [(ngModel)]="templateId" (ngModelChange)="render()">
                  <option [ngValue]="null">Write my own message</option>
                  @for (g of grouped(); track g.category) {
                    <optgroup [label]="categoryLabels[g.category]">
                      @for (t of g.templates; track t.crmWhatsAppTemplateId) { <option [ngValue]="t.crmWhatsAppTemplateId">{{ t.name }}</option> }
                    </optgroup>
                  }
                </select>
              </div>
              @if (interests.length > 1) {
                <div class="fld">
                  <span class="lbl">About</span>
                  <select class="inp" [(ngModel)]="interestId" (ngModelChange)="render()">
                    <option [ngValue]="null">Their latest open enquiry</option>
                    @for (i of interests; track i.customerTripInterestId) { <option [ngValue]="i.customerTripInterestId">{{ i.tripTitle }}</option> }
                  </select>
                </div>
              }
            </div>

            <div class="crm-wa-to">
              To <b>{{ preview()?.customerName || customerName || 'Customer' }}</b>
              @if (preview()?.whatsAppPhone; as p) { · +{{ p.slice(0, 2) }} {{ p.slice(2, 7) }} {{ p.slice(7) }} }
            </div>

            @if (loading()) {
              <p class="note" style="margin:10px 0">Filling in the template…</p>
            }
            <textarea class="inp crm-wa-text" rows="12" [(ngModel)]="message" maxlength="4000" placeholder="Type your message"></textarea>
            <div class="note" style="text-align:right;margin-top:2px">{{ message.length }}/4000</div>

            @if (preview()?.missingValues?.length) {
              <div class="warnbox" style="margin:8px 0">
                No value for {{ missingText() }} for this customer, so it was left out. Check the message before sending.
              </div>
            }
            @if (preview() && !preview()!.whatsAppPhone) {
              <div class="errorbox" style="margin:8px 0">This customer has no valid mobile number. Add one to their profile first.</div>
            } @else if (preview() && !preview()!.whatsAppAvailable) {
              <div class="errorbox" style="margin:8px 0">This customer is marked as not on WhatsApp. Edit their profile if that has changed.</div>
            }
            @if (error()) { <div class="errorbox" style="margin:8px 0">{{ error() }}</div> }

            <p class="note" style="margin:8px 0 12px">Nothing is sent from here — WhatsApp opens with this message ready and you press Send.</p>
            <div class="crm-wa-buttons">
              <button type="button" class="btn ghost" (click)="close()">Cancel</button>
              <button type="button" class="btn ghost crm-wa-app" [disabled]="!canOpen()" (click)="openWhatsApp('app')">Open WhatsApp app</button>
              <button type="button" class="btn crm-wa-go" [disabled]="!canOpen()" (click)="openWhatsApp('web')">
                @if (opening()) { <span class="spin"></span> } @else { Open WhatsApp Web }
              </button>
            </div>
          } @else {
            <h4>Did you send it?</h4>
            <p class="note" style="margin-bottom:12px">
              WhatsApp opened with the message for {{ customerName || 'the customer' }}. The CRM can't see WhatsApp, so it only
              records "Opened WhatsApp" until you confirm you pressed Send.
            </p>
            @if (fallbackUrl()) {
              <div class="warnbox" style="margin-bottom:12px">
                Your browser blocked the new tab. <a [href]="fallbackUrl()" target="_blank" rel="noopener">Open WhatsApp</a>
              </div>
            }
            @if (error()) { <div class="errorbox" style="margin-bottom:12px">{{ error() }}</div> }
            <div class="crm-wa-buttons">
              <button type="button" class="btn ghost" (click)="finish(null)">Decide later</button>
              <button type="button" class="btn ghost" [disabled]="saving()" (click)="finish(false)">Not sent</button>
              <button type="button" class="btn" [disabled]="saving()" (click)="finish(true)">
                @if (saving()) { <span class="spin"></span> } @else { Mark as sent }
              </button>
            </div>
          }
        </div>
      </div>
    }
  `,
  styleUrls: ['../crm-shared.css'],
})
export class CrmWhatsAppDialogComponent implements OnChanges {
  private readonly crm = inject(AdminCrmService);

  @Input() open = false;
  @Input() customerId: number | null = null;
  @Input() customerName: string | null = null;
  /** The trip the message is about; null uses the customer's latest open enquiry. */
  @Input() interestIdPreset: number | null = null;
  /** The customer's trip interests, to choose between when there are several. */
  @Input() interests: CrmTripInterest[] = [];
  /** Template category to pre-select (e.g. Follow-up from the follow-ups list). */
  @Input() suggestedCategory: CrmTemplateCategory | null = null;
  @Output() closed = new EventEmitter<void>();
  /** A message was opened in WhatsApp (whatever the admin then said about sending it). */
  @Output() logged = new EventEmitter<void>();

  readonly categoryLabels = CrmTemplateCategoryLabels;
  readonly templates = signal<CrmTemplate[]>([]);
  readonly preview = signal<CrmRenderedMessage | null>(null);
  readonly loading = signal(false);
  readonly opening = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly step = signal<'compose' | 'confirm'>('compose');
  readonly fallbackUrl = signal<string | null>(null);

  templateId: number | null = null;
  interestId: number | null = null;
  message = '';
  private communicationId: number | null = null;

  readonly grouped = computed(() => {
    const groups = new Map<CrmTemplateCategory, CrmTemplate[]>();
    for (const t of this.templates()) groups.set(t.category, [...(groups.get(t.category) ?? []), t]);
    return [...groups.entries()].sort((a, b) => a[0] - b[0]).map(([category, templates]) => ({ category, templates }));
  });

  missingText(): string {
    return (this.preview()?.missingValues ?? []).map((v) => `{${v}}`).join(', ');
  }

  canOpen(): boolean {
    const p = this.preview();
    return !!this.message.trim() && !this.opening() && !this.loading() && !!p?.whatsAppPhone && p.whatsAppAvailable;
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Reset only when the dialog opens — the page behind it refreshes (new inputs) while it's open.
    if (!changes['open'] || !this.open || changes['open'].previousValue) return;
    this.step.set('compose');
    this.error.set(null);
    this.fallbackUrl.set(null);
    this.preview.set(null);
    this.message = '';
    this.communicationId = null;
    this.interestId = this.interestIdPreset;
    this.templateId = null;
    this.crm.templates(true).subscribe({
      next: (list) => {
        this.templates.set(list);
        const suggested = this.suggestedCategory !== null ? list.find((t) => t.category === this.suggestedCategory) : undefined;
        this.templateId = (suggested ?? list[0])?.crmWhatsAppTemplateId ?? null;
        this.render();
      },
      error: () => this.render(),
    });
  }

  /** Fill the chosen template on the server; with "Write my own", just check the customer's number. */
  render(): void {
    if (!this.customerId) return;
    this.loading.set(true);
    this.error.set(null);
    // Own message: the call only checks the customer's number, so any non-empty body will do.
    const body = this.templateId ? null : '{name}';
    this.crm.previewMessage(this.customerId, this.interestId, this.templateId, body).subscribe({
      next: (p) => {
        this.loading.set(false);
        this.preview.set(p);
        if (this.templateId) this.message = p.message;
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(apiErrorMessage(err, 'Could not prepare the message.'));
      },
    });
  }

  /**
   * Opens the tab straight away (inside the click, so the browser doesn't block it), records
   * "Opened WhatsApp" on the server, then points the tab at WhatsApp with the message.
   */
  openWhatsApp(kind: 'web' | 'app'): void {
    if (!this.customerId || !this.canOpen()) return;
    const tab = window.open('', '_blank');
    this.opening.set(true);
    this.error.set(null);
    this.crm.openWhatsApp(this.customerId, this.interestId, this.templateId, this.message.trim()).subscribe({
      next: (link) => {
        this.opening.set(false);
        this.communicationId = link.crmCommunicationId;
        const url = kind === 'web' ? link.webUrl : link.appUrl;
        if (tab) {
          tab.opener = null;
          tab.location.href = url;
        } else {
          this.fallbackUrl.set(url);
        }
        this.step.set('confirm');
        this.logged.emit();
      },
      error: (err) => {
        this.opening.set(false);
        tab?.close();
        this.error.set(apiErrorMessage(err, 'Could not open WhatsApp.'));
      },
    });
  }

  /** true = Mark as sent, false = Not sent, null = decide later (stays "Opened WhatsApp"). */
  finish(sent: boolean | null): void {
    if (sent === null || !this.communicationId) {
      this.close();
      return;
    }
    this.saving.set(true);
    const request$ = sent ? this.crm.markSent(this.communicationId) : this.crm.markNotSent(this.communicationId);
    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.logged.emit();
        this.close();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err, 'Could not save that.'));
      },
    });
  }

  close(): void {
    this.closed.emit();
  }
}
