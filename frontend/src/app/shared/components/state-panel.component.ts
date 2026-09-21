import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-state-panel',
  standalone: true,
  template: `
    <div class="state-panel">
      @if (kind === 'loading') {
        <span class="spin"></span>
        <p>{{ message || 'Loading…' }}</p>
      } @else if (kind === 'error') {
        <p class="mut">{{ message || 'Something went wrong.' }}</p>
      } @else {
        <p class="mut">{{ message || 'Nothing here yet.' }}</p>
      }
    </div>
  `,
})
export class StatePanelComponent {
  @Input({ required: true }) kind!: 'loading' | 'empty' | 'error';
  @Input() message?: string;
}
