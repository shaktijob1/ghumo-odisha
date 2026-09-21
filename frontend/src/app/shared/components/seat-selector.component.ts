import { Component, input, model } from '@angular/core';

@Component({
  selector: 'app-seat-selector',
  standalone: true,
  template: `
    <div class="stepper">
      <button type="button" (click)="decrement()" [disabled]="value() <= 1">−</button>
      <b>{{ value() }}</b>
      <button type="button" (click)="increment()" [disabled]="value() >= max()">+</button>
    </div>
  `,
})
export class SeatSelectorComponent {
  readonly max = input(10);
  readonly value = model(1);

  increment(): void {
    if (this.value() < this.max()) this.value.set(this.value() + 1);
  }

  decrement(): void {
    if (this.value() > 1) this.value.set(this.value() - 1);
  }
}
