import { Component, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

/**
 * Confirm dialog for an admin decision (approve / reject / suspend / deactivate / cancel …).
 * The reason box is required when `reasonRequired` is set — the server enforces the same rule and
 * shows the reason to the driver or customer.
 */
@Component({
  selector: 'app-decision-dialog',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="modal-backdrop" (click)="closed.emit()">
      <div class="modal" role="dialog" aria-modal="true" [attr.aria-label]="title()" (click)="$event.stopPropagation()">
        <h4>{{ title() }}</h4>
        @if (message()) { <p class="note" style="margin-bottom:14px">{{ message() }}</p> }
        <ng-content></ng-content>
        @if (reasonLabel()) {
          <div class="fld">
            <label class="lbl" for="dd-reason">{{ reasonLabel() }}{{ reasonRequired() ? '' : ' (optional)' }}</label>
            <textarea id="dd-reason" class="inp" rows="3" maxlength="500" [(ngModel)]="reason" [placeholder]="reasonPlaceholder()"></textarea>
          </div>
        }
        @if (localError() || error()) { <div class="errorbox">{{ localError() || error() }}</div> }
        <div class="row" style="gap:8px">
          <button type="button" class="btn ghost" style="flex:1" (click)="closed.emit()">Close</button>
          <button type="button" class="btn" style="flex:1" [class.dang]="danger()" [disabled]="busy()" (click)="submit()">
            @if (busy()) { <span class="spin"></span> } @else { {{ confirmLabel() }} }
          </button>
        </div>
      </div>
    </div>
  `,
})
export class DecisionDialogComponent {
  readonly title = input.required<string>();
  readonly message = input<string | null>(null);
  readonly confirmLabel = input('Confirm');
  /** Empty = no reason box. */
  readonly reasonLabel = input<string | null>('Reason');
  readonly reasonPlaceholder = input('');
  readonly reasonRequired = input(false);
  readonly danger = input(false);
  readonly busy = input(false);
  readonly error = input<string | null>(null);
  readonly confirmed = output<string | null>();
  readonly closed = output<void>();

  reason = '';
  readonly localError = signal<string | null>(null);

  constructor() {
    // A fresh dialog title means a fresh decision — clear what was typed for the last one.
    effect(() => {
      this.title();
      this.reason = '';
      this.localError.set(null);
    });
  }

  submit(): void {
    const reason = this.reason.trim();
    if (this.reasonRequired() && !reason) {
      this.localError.set('Please give a reason. The driver or customer will see it.');
      return;
    }
    this.localError.set(null);
    this.confirmed.emit(reason || null);
  }
}
