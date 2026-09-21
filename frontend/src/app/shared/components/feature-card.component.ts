import { Component, Input } from '@angular/core';

export type FeatureIcon = 'compass' | 'users' | 'map-pin' | 'luggage' | 'chat' | 'life-buoy';

@Component({
  selector: 'app-feature-card',
  standalone: true,
  template: `
    <div class="featcard">
      <div class="fic">
        @switch (icon) {
          @case ('compass') {
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="12" r="9"></circle>
              <path d="M15 9l-2 6-6 2 2-6 6-2z"></path>
            </svg>
          }
          @case ('users') {
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
              <path d="M16 19v-1.5a3.5 3.5 0 0 0-3.5-3.5h-5A3.5 3.5 0 0 0 4 17.5V19"></path>
              <circle cx="9" cy="7.5" r="3"></circle>
              <path d="M18 19v-1.5a3.5 3.5 0 0 0-2.3-3.29"></path>
              <path d="M14.5 4.3a3 3 0 0 1 0 5.8"></path>
            </svg>
          }
          @case ('map-pin') {
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
              <path d="M12 21s-6.5-5.9-6.5-11A6.5 6.5 0 0 1 18.5 10c0 5.1-6.5 11-6.5 11z"></path>
              <circle cx="12" cy="10" r="2.3"></circle>
            </svg>
          }
          @case ('luggage') {
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
              <rect x="4" y="8" width="16" height="12" rx="2"></rect>
              <path d="M9 8V5.5A1.5 1.5 0 0 1 10.5 4h3A1.5 1.5 0 0 1 15 5.5V8"></path>
              <path d="M9 12v4M15 12v4"></path>
            </svg>
          }
          @case ('chat') {
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
              <path d="M21 11.5a7.5 7.5 0 0 1-11.2 6.5L4 19l1.1-4.2A7.5 7.5 0 1 1 21 11.5z"></path>
            </svg>
          }
          @case ('life-buoy') {
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="12" cy="12" r="9"></circle>
              <circle cx="12" cy="12" r="3.3"></circle>
              <path d="M7 7l2.6 2.6M17 7l-2.6 2.6M7 17l2.6-2.6M17 17l-2.6-2.6"></path>
            </svg>
          }
        }
      </div>
      <b>{{ title }}</b>
      <p>{{ description }}</p>
    </div>
  `,
})
export class FeatureCardComponent {
  @Input({ required: true }) icon!: FeatureIcon;
  @Input({ required: true }) title!: string;
  @Input({ required: true }) description!: string;
}
