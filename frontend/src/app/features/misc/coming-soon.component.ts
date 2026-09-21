import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-coming-soon',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="container" style="padding:60px 0;text-align:center">
      <div class="eyebrow" style="margin-bottom:8px">Coming soon</div>
      <h1 style="font-size:26px;margin-bottom:10px">{{ title }}</h1>
      <p class="mut" style="max-width:48ch;margin:0 auto">We're working on {{ title | lowercase }} bookings. For now, browse our trips or contact us directly to plan your trip.</p>
    </div>
  `,
})
export class ComingSoonComponent {
  @Input() title = 'Coming soon';
}
