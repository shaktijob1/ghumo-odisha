import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

export interface Crumb {
  label: string;
  /** Omit for the current page (the last crumb). */
  link?: string;
}

/**
 * Visible "Home › Trips › Koraput › Koraput Explore" trail. The API writes the same trail as
 * BreadcrumbList structured data, so search results can show it too.
 */
@Component({
  selector: 'app-breadcrumbs',
  standalone: true,
  imports: [RouterLink],
  template: `
    <nav class="crumb" aria-label="Breadcrumb">
      <ol>
        <li><a routerLink="/">Home</a></li>
        @for (c of items(); track c.label; let last = $last) {
          <li>
            @if (c.link && !last) {
              <a [routerLink]="c.link">{{ c.label }}</a>
            } @else {
              <span class="now" aria-current="page">{{ c.label }}</span>
            }
          </li>
        }
      </ol>
    </nav>
  `,
  styles: `
    .crumb ol { display: flex; flex-wrap: wrap; align-items: center; gap: 7px; list-style: none; margin: 0; padding: 0; }
    .crumb li { display: inline-flex; align-items: center; gap: 7px; min-width: 0; }
    .crumb li + li::before { content: "›"; color: var(--muted); }
    .crumb a { color: var(--muted); }
    .crumb a:hover { color: var(--accent); }
  `,
})
export class BreadcrumbsComponent {
  readonly items = input.required<Crumb[]>();
}
