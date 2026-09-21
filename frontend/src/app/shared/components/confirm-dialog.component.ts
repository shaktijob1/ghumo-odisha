import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  template: `
    @if (open) {
      <div class="modal-backdrop" (click)="cancel.emit()">
        <div class="modal" (click)="$event.stopPropagation()">
          <h4>{{ title }}</h4>
          <p class="note" style="margin-bottom:16px">{{ message }}</p>
          @if (danger) {
            <div class="warnbox">{{ danger }}</div>
          }
          <div class="row" style="gap:8px">
            <button class="btn ghost" style="flex:1" (click)="cancel.emit()">{{ cancelLabel }}</button>
            <button class="btn" [class.dang]="destructive" style="flex:1" (click)="confirm.emit()">{{ confirmLabel }}</button>
          </div>
        </div>
      </div>
    }
  `,
})
export class ConfirmDialogComponent {
  @Input() open = false;
  @Input() title = 'Are you sure?';
  @Input() message = '';
  @Input() danger?: string;
  @Input() confirmLabel = 'Confirm';
  @Input() cancelLabel = 'Cancel';
  @Input() destructive = false;

  @Output() confirm = new EventEmitter<void>();
  @Output() cancel = new EventEmitter<void>();
}
