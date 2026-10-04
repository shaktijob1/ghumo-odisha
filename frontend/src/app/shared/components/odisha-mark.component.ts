import { Component } from '@angular/core';

/**
 * Small colourful map of Odisha shown before the "Ghumo Odisha" name in the navbar: green hills
 * in the west fading to the saffron coast, with a pin on Bhubaneswar where trips start.
 * Outline simplified from the DataMeet India state boundaries (CC BY 2.5 IN).
 */
@Component({
  selector: 'app-odisha-mark',
  standalone: true,
  template: `
    <svg class="om" viewBox="-3 -3 106 89" aria-hidden="true" focusable="false">
      <defs>
        <linearGradient id="om-grad" gradientUnits="userSpaceOnUse" x1="20" y1="12" x2="78" y2="62">
          <stop offset="0" stop-color="#0F6F5C" />
          <stop offset=".38" stop-color="#22A884" />
          <stop offset=".7" stop-color="#F4B23E" />
          <stop offset="1" stop-color="#EE6A2A" />
        </linearGradient>
      </defs>
      <path d="M76.9 0.0L91.5 7.9L92.5 11.6L95.9 10.1L96.5 12.7L99.7 14.1L100.0 16.3L93.1 18.8L89.8 23.0L91.6 30.0L88.6 31.3L92.9 31.8L87.5 36.0L88.4 38.7L81.7 45.2L66.5 50.8L55.4 60.5L53.8 59.0L52.5 61.5L49.7 61.5L48.0 65.6L41.0 65.1L36.7 59.1L35.2 61.6L34.0 60.5L33.9 62.7L31.5 61.9L33.2 64.2L27.3 68.5L27.4 72.7L23.2 71.6L20.4 75.2L17.9 69.9L14.5 79.5L10.5 78.2L3.7 82.5L0.0 82.6L2.3 74.7L5.9 73.2L9.2 69.5L8.2 67.9L11.4 66.8L14.0 63.3L13.1 54.5L10.4 53.0L11.1 48.1L7.6 45.9L7.9 43.7L9.1 42.5L13.9 44.3L15.6 47.3L17.3 46.0L19.8 46.6L19.7 48.3L21.6 47.2L21.7 44.4L16.5 43.2L15.8 28.8L17.6 29.9L20.5 24.2L28.4 25.0L30.9 20.2L33.1 20.7L32.0 18.1L36.1 12.0L35.2 8.7L36.7 5.7L42.9 2.9L42.8 0.1L47.6 3.4L60.3 0.9L61.1 4.2L59.1 7.9L63.0 9.3L65.2 6.6L70.4 8.4L72.4 7.3L71.7 9.5L74.1 9.9L76.4 3.9L75.0 0.9Z" fill="url(#om-grad)" stroke="#fff" stroke-width="2" stroke-linejoin="round" />
      <circle cx="72.7" cy="39.2" r="9" fill="#fff" />
      <circle cx="72.7" cy="39.2" r="5" fill="#E2412F" />
    </svg>
  `,
  styles: `
    :host { display: inline-flex; flex-shrink: 0; }
    .om { display: block; width: 34px; height: auto; filter: drop-shadow(0 1px 2px rgba(15, 20, 22, .18)); }
    @media (max-width: 640px) { .om { width: 30px; } }
  `,
})
export class OdishaMarkComponent {}
