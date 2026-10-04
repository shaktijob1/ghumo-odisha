import { Component, input } from '@angular/core';
import { FaqItem } from '../../core/models/trip.model';

/**
 * Questions and answers matching the FAQPage structured data the API writes for the same page.
 * Each is a tap-to-open question; the answers stay in the page while closed, so Google and
 * screen readers still read them.
 */
@Component({
  selector: 'app-faq-list',
  standalone: true,
  template: `
    <div class="faqlist">
      @for (f of faqs(); track f.question) {
        <details class="faqitem faqx">
          <summary>
            <span>{{ f.question }}</span>
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"></path></svg>
          </summary>
          <p>{{ f.answer }}</p>
        </details>
      }
    </div>
  `,
  styles: `
    .faqlist { display: grid; gap: 10px; margin: 0; }
    .faqitem { border: 1px solid var(--line); border-radius: var(--radius-control); padding: 14px 16px; background: #fff; }
    .faqx { padding: 0; }
    .faqx summary { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 13px 16px; cursor: pointer; list-style: none; font-weight: 600; font-size: 14px; color: var(--ink); }
    .faqx summary::-webkit-details-marker { display: none; }
    .faqx summary svg { flex-shrink: 0; color: var(--accent); transition: transform .2s ease; }
    .faqx[open] summary svg { transform: rotate(180deg); }
    .faqx[open] summary { color: var(--accent); }
    .faqx summary:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; border-radius: var(--radius-control); }
    .faqx p { margin: 0; padding: 0 16px 14px; font-size: 13.5px; line-height: 1.65; color: var(--muted); }
  `,
})
export class FaqListComponent {
  readonly faqs = input.required<FaqItem[]>();
}
