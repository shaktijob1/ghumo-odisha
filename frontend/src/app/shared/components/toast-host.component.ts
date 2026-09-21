import { Component, inject } from '@angular/core';
import { ToastService } from '../../core/services/toast.service';

@Component({
  selector: 'app-toast-host',
  standalone: true,
  template: `
    <div class="toast-stack">
      @for (t of toastService.toasts(); track t.id) {
        <div class="toast" [class]="t.kind" (click)="toastService.dismiss(t.id)">{{ t.message }}</div>
      }
    </div>
  `,
})
export class ToastHostComponent {
  readonly toastService = inject(ToastService);
}
