import { Component, EventEmitter, Input, OnChanges, Output, inject, signal, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { apiErrorMessage } from '../../../../shared/utils/api-error';

/** Quick "Add note" for a customer, optionally about one of their trip interests. */
@Component({
  selector: 'app-crm-note-dialog',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (open) {
      <div class="modal-backdrop" (click)="closed.emit()">
        <div class="modal" (click)="$event.stopPropagation()">
          <h4>Add note{{ subject ? ' · ' + subject : '' }}</h4>
          <textarea class="inp" rows="4" [(ngModel)]="text" maxlength="2000" placeholder="Asked about pickup from Patia" style="margin-bottom:12px"></textarea>
          @if (error()) { <div class="errorbox" style="margin-bottom:12px">{{ error() }}</div> }
          <div class="row" style="gap:8px">
            <button type="button" class="btn ghost" style="flex:1" (click)="closed.emit()">Cancel</button>
            <button type="button" class="btn" style="flex:1" [disabled]="saving() || !text.trim()" (click)="save()">
              @if (saving()) { <span class="spin"></span> } @else { Add note }
            </button>
          </div>
        </div>
      </div>
    }
  `,
})
export class CrmNoteDialogComponent implements OnChanges {
  private readonly crm = inject(AdminCrmService);

  @Input() open = false;
  @Input() customerId: number | null = null;
  @Input() interestId: number | null = null;
  @Input() subject: string | null = null;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  text = '';

  ngOnChanges(changes: SimpleChanges): void {
    // Reset only when the dialog opens — the page behind may pass new inputs while it is open.
    if (!changes['open']?.currentValue || changes['open'].previousValue) return;
    if (!this.open) return;
    this.text = '';
    this.error.set(null);
  }

  save(): void {
    if (!this.customerId || !this.text.trim()) return;
    this.saving.set(true);
    this.crm.addNote(this.customerId, this.text.trim(), this.interestId).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.emit();
      },
      error: (err) => {
        this.saving.set(false);
        this.error.set(apiErrorMessage(err, 'Could not save the note.'));
      },
    });
  }
}
