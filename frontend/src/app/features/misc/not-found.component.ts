import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DestinationSummary } from '../../core/models/destination.model';
import { PublicDestinationService } from '../../core/services/public-destination.service';

/**
 * Unknown addresses. The server answers these with HTTP 404 and noindex (SeoPageRenderer); this
 * page gives the visitor somewhere useful to go next.
 */
@Component({
  selector: 'app-not-found',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="container nf">
      <h1>Page not found</h1>
      <p class="mut">The page you're looking for doesn't exist or has moved.</p>
      <div class="nf-actions">
        <a class="btn" routerLink="/">Back to home</a>
        <a class="btn ghost" routerLink="/" fragment="upcoming-trips">Browse all trips</a>
      </div>
      @if (destinations().length > 0) {
        <h2>Popular destinations</h2>
        <ul class="nf-dest">
          @for (d of destinations(); track d.slug) {
            <li><a [routerLink]="['/destinations', d.slug]">{{ d.name }}</a></li>
          }
        </ul>
      }
    </div>
  `,
  styles: `
    .nf { padding: 72px 20px; text-align: center; }
    .nf h1 { font-size: 32px; margin-bottom: 10px; }
    .nf h2 { font-size: 18px; margin: 36px 0 12px; }
    .nf-actions { display: flex; flex-wrap: wrap; gap: 10px; justify-content: center; margin-top: 20px; }
    .nf-dest { display: flex; flex-wrap: wrap; gap: 8px; justify-content: center; list-style: none; margin: 0; padding: 0; }
    .nf-dest a { display: inline-block; padding: 8px 14px; border: 1px solid var(--line); border-radius: var(--radius-pill); background: #fff; font-size: 13.5px; font-weight: 600; color: var(--ink); }
    .nf-dest a:hover { border-color: #BFDDD4; color: var(--accent); }
  `,
})
export class NotFoundComponent {
  readonly destinations = signal<DestinationSummary[]>([]);

  constructor() {
    inject(PublicDestinationService).getTrendingDestinations().subscribe({
      next: (d) => this.destinations.set(d),
      error: () => undefined,
    });
  }
}
