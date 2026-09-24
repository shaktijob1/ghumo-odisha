import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DestinationSummary } from '../../core/models/destination.model';
import { ImageUrlPipe } from '../pipes/image-url.pipe';

@Component({
  selector: 'app-destination-card',
  standalone: true,
  imports: [CommonModule, RouterLink, ImageUrlPipe],
  template: `
    <a class="dcard" [routerLink]="['/destinations', destination.slug]">
      <div class="ph">
        @if (destination.heroImageUrl) {
          <img [src]="destination.heroImageUrl | imageUrl" alt="{{ destination.name }}" />
        }
      </div>
      <div class="cnt">
        <span>{{ destination.tripCount }} trip{{ destination.tripCount === 1 ? '' : 's' }}</span>
      </div>
      <div class="info">
        <b>{{ destination.name }}</b>
        @if (destination.startingPrice !== null) {
          <span>Starting at &#8377;{{ destination.startingPrice | number: '1.0-0' }}*</span>
        }
      </div>
    </a>
  `,
})
export class DestinationCardComponent {
  @Input({ required: true }) destination!: DestinationSummary;
}
