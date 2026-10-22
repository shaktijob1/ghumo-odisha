import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { ToastService } from '../../../../core/services/toast.service';
import { CrmTemplate, CrmTemplateCategory, CrmTemplateCategoryLabels } from '../../../../core/models/crm.model';
import { ConfirmDialogComponent } from '../../../../shared/components/confirm-dialog.component';
import { StatePanelComponent } from '../../../../shared/components/state-panel.component';
import { apiErrorMessage } from '../../../../shared/utils/api-error';

/**
 * Example values for the editor's "sample preview" only — so the admin sees roughly how a template
 * reads. Real messages are always filled on the server from the customer's own trip and booking.
 */
const SAMPLE: Record<string, string> = {
  name: 'Rahul',
  trip: 'Koraput + Araku',
  date: '14-Nov-2026',
  return_date: '16-Nov-2026',
  price: '5,499',
  booking_amount: '198',
  available_seats: '6',
  total_seats: '16',
  travellers: '2',
  pickup: 'Patia',
  vehicle: 'AC Traveller',
  stay: 'AC Stay',
  balance: '10,800',
  booking_id: 'GO-123456',
};

/** Admin → CRM → WhatsApp Templates: create, edit, duplicate, switch on/off and delete. */
@Component({
  selector: 'app-crm-templates',
  standalone: true,
  imports: [CommonModule, FormsModule, StatePanelComponent, ConfirmDialogComponent],
  templateUrl: './crm-templates.component.html',
  styleUrls: ['../crm-shared.css', './crm-templates.component.css'],
})
export class CrmTemplatesComponent implements OnInit {
  private readonly crm = inject(AdminCrmService);
  private readonly toast = inject(ToastService);

  @ViewChild('bodyBox') bodyBox?: ElementRef<HTMLTextAreaElement>;

  readonly categoryLabels = CrmTemplateCategoryLabels;
  readonly categories = Object.values(CrmTemplateCategory).filter((v): v is CrmTemplateCategory => typeof v === 'number');

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly templates = signal<CrmTemplate[]>([]);
  readonly placeholders = signal<string[]>([]);
  readonly filterCategory = signal<CrmTemplateCategory | null>(null);
  readonly showInactive = signal(true);

  // Editor
  readonly editorOpen = signal(false);
  readonly editing = signal<CrmTemplate | null>(null);
  readonly saving = signal(false);
  readonly editorError = signal<string | null>(null);
  name = '';
  category = CrmTemplateCategory.FollowUp;
  body = '';
  isActive = true;

  readonly deleteTarget = signal<CrmTemplate | null>(null);

  readonly visible = computed(() => {
    const category = this.filterCategory();
    return this.templates().filter((t) => (category === null || t.category === category) && (this.showInactive() || t.isActive));
  });

  ngOnInit(): void {
    this.crm.placeholders().subscribe({ next: (p) => this.placeholders.set(p), error: () => undefined });
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.crm.templates(false).subscribe({
      next: (list) => {
        this.templates.set(list);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  openEditor(template: CrmTemplate | null): void {
    this.editing.set(template);
    this.name = template?.name ?? '';
    this.category = template?.category ?? CrmTemplateCategory.FollowUp;
    this.body = template?.body ?? '';
    this.isActive = template?.isActive ?? true;
    this.editorError.set(null);
    this.editorOpen.set(true);
  }

  /** Put {placeholder} where the cursor is in the message box. */
  insert(placeholder: string): void {
    const box = this.bodyBox?.nativeElement;
    const token = `{${placeholder}}`;
    if (!box) {
      this.body += token;
      return;
    }
    // Straight into the box (not via the model), so typing that follows can't overwrite it.
    box.focus();
    box.setRangeText(token, box.selectionStart ?? box.value.length, box.selectionEnd ?? box.value.length, 'end');
    this.body = box.value;
  }

  /** Placeholders typed that don't exist ({nmae}) — the server refuses them too. */
  unknownPlaceholders(): string[] {
    const known = new Set(this.placeholders());
    const found = [...this.body.matchAll(/\{([a-zA-Z_]+)\}/g)].map((m) => m[1].toLowerCase());
    return [...new Set(found.filter((p) => known.size > 0 && !known.has(p)))];
  }

  samplePreview(body: string): string {
    return body.replace(/\{([a-zA-Z_]+)\}/g, (match, key: string) => SAMPLE[key.toLowerCase()] ?? match);
  }

  save(): void {
    if (!this.name.trim() || !this.body.trim()) {
      this.editorError.set('Give the template a name and a message.');
      return;
    }
    const request = { name: this.name.trim(), category: this.category, body: this.body, isActive: this.isActive };
    const editing = this.editing();
    this.saving.set(true);
    (editing ? this.crm.updateTemplate(editing.crmWhatsAppTemplateId, request) : this.crm.createTemplate(request)).subscribe({
      next: () => {
        this.saving.set(false);
        this.editorOpen.set(false);
        this.toast.success(editing ? 'Template saved.' : 'Template created.');
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.editorError.set(apiErrorMessage(err, 'Could not save the template.'));
      },
    });
  }

  toggleActive(t: CrmTemplate): void {
    this.crm.setTemplateActive(t.crmWhatsAppTemplateId, !t.isActive).subscribe({
      next: () => {
        this.toast.success(t.isActive ? 'Template switched off.' : 'Template switched on.');
        this.load();
      },
      error: (err) => this.toast.error(apiErrorMessage(err, 'Could not change the template.')),
    });
  }

  duplicate(t: CrmTemplate): void {
    this.crm.duplicateTemplate(t.crmWhatsAppTemplateId).subscribe({
      next: (copy) => {
        this.toast.success('Copied — it starts switched off.');
        this.load();
        this.openEditor(copy);
      },
      error: (err) => this.toast.error(apiErrorMessage(err, 'Could not copy the template.')),
    });
  }

  confirmDelete(): void {
    const t = this.deleteTarget();
    if (!t) return;
    this.crm.deleteTemplate(t.crmWhatsAppTemplateId).subscribe({
      next: () => {
        this.deleteTarget.set(null);
        this.toast.success('Template deleted.');
        this.load();
      },
      error: (err) => {
        this.deleteTarget.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not delete the template.'));
      },
    });
  }
}
