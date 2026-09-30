import { Component, input, model } from '@angular/core';

@Component({
  selector: 'app-seat-selector',
  standalone: true,
  template: `
    <div class="stepper">
      <button type="button" (click)="decrement()" [disabled]="value() <= min()" [attr.aria-label]="'Fewer ' + label()">−</button>
      <b>{{ value() }}</b>
      <button type="button" (click)="increment()" [disabled]="value() >= max()" [attr.aria-label]="'More ' + label()">+</button>
    </div>
  `,
})
export class SeatSelectorComponent {
  readonly max = input(10);
  readonly min = input(1);
  /** What's being counted, for screen readers ("travellers", "gents"…). */
  readonly label = input('travellers');
  readonly value = model(1);

  increment(): void {
    if (this.value() < this.max()) this.value.set(this.value() + 1);
  }

  decrement(): void {
    if (this.value() > this.min()) this.value.set(this.value() - 1);
  }
}
