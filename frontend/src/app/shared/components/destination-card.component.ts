import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-destination-card',
  standalone: true,
  template: `
    <div class="destcard" [style.background]="gradient">
      <svg class="ic" width="46" height="46" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linecap="round" stroke-linejoin="round">
        <path d="M3 18l5.5-7 4 5 2.5-3 6 5"></path>
      </svg>
      <b>{{ name }}</b>
      <span>{{ tagline }}</span>
    </div>
  `,
})
export class DestinationCardComponent {
  @Input({ required: true }) name!: string;
  @Input({ required: true }) tagline!: string;
  @Input({ required: true }) gradient!: string;
}
