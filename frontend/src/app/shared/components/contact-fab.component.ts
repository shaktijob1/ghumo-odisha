import { Component, computed, inject } from '@angular/core';
import { ContactService } from '../../core/services/contact.service';

/**
 * Floating Call + WhatsApp + Instagram buttons pinned to the bottom-right corner of every
 * customer page. Numbers come from the API (/contact → OrganizerContactOptions),
 * never hardcoded. A soft pulsing ring makes them "blink" to draw the eye.
 */
@Component({
  selector: 'app-contact-fab',
  standalone: true,
  template: `
    @if (whatsAppLink() || telLink() || instagramUrl()) {
      <div class="cfab" role="group" aria-label="Contact us">
        @if (telLink(); as tel) {
          <a class="cfab-btn cfab-call" [href]="tel" aria-label="Call us" title="Call us">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M6.6 10.8a15.1 15.1 0 0 0 6.6 6.6l2.2-2.2c.3-.3.7-.4 1-.2 1.1.4 2.3.6 3.6.6.6 0 1 .4 1 1V20c0 .6-.4 1-1 1A17 17 0 0 1 3 4c0-.6.4-1 1-1h3.5c.6 0 1 .4 1 1 0 1.3.2 2.5.6 3.6.1.3 0 .7-.2 1l-2.3 2.2z"></path></svg>
          </a>
        }
        @if (whatsAppLink(); as wa) {
          <a class="cfab-btn cfab-wa" [href]="wa" target="_blank" rel="noopener" aria-label="Chat with us on WhatsApp" title="WhatsApp">
            <svg width="26" height="26" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M12 2a10 10 0 0 0-8.6 15.1L2 22l5-1.3A10 10 0 1 0 12 2zm0 18.2a8.2 8.2 0 0 1-4.2-1.2l-.3-.2-3 .8.8-2.9-.2-.3A8.2 8.2 0 1 1 12 20.2zm4.5-6.1c-.2-.1-1.5-.7-1.7-.8-.2-.1-.4-.1-.6.1l-.8 1c-.1.2-.3.2-.5.1a6.7 6.7 0 0 1-3.3-2.9c-.2-.4.2-.4.7-1.3.1-.2 0-.3 0-.4l-.8-1.8c-.2-.5-.4-.4-.6-.4h-.5a1 1 0 0 0-.7.3 3 3 0 0 0-.9 2.2 5.2 5.2 0 0 0 1.1 2.7 11.8 11.8 0 0 0 4.5 4c1.7.7 2.3.8 3.2.7a2.7 2.7 0 0 0 1.8-1.3 2.2 2.2 0 0 0 .2-1.3c-.1-.1-.3-.2-.6-.3z"></path></svg>
          </a>
        }
        @if (instagramUrl(); as ig) {
          <a class="cfab-btn cfab-ig" [href]="ig" target="_blank" rel="noopener" aria-label="Follow us on Instagram" title="Instagram">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="5"></rect><circle cx="12" cy="12" r="4"></circle><circle cx="17.5" cy="6.5" r="1" fill="currentColor" stroke="none"></circle></svg>
          </a>
        }
      </div>
    }
  `,
  styles: `
    .cfab { position: fixed; right: 20px; bottom: 24px; z-index: 40; display: flex; flex-direction: column; gap: 14px; }
    .cfab-btn { position: relative; display: grid; place-items: center; width: 54px; height: 54px; border-radius: 999px; color: #fff; box-shadow: 0 8px 22px rgba(15,20,22,.22); transition: transform .18s ease; }
    .cfab-btn:hover { transform: scale(1.07); }
    .cfab-btn:focus-visible { outline: 3px solid #BFDDD4; outline-offset: 3px; }
    .cfab-btn svg { position: relative; z-index: 1; }
    .cfab-call { background: var(--accent); }
    .cfab-wa { background: #25D366; }
    /* Instagram's own gradient. */
    .cfab-ig { background: radial-gradient(circle at 30% 107%, #FDF497 0%, #FDF497 5%, #FD5949 45%, #D6249F 60%, #285AEB 90%); }
    /* Pulsing ring — the "blink", staggered down the stack. */
    .cfab-btn::before { content: ''; position: absolute; inset: 0; border-radius: inherit; background: inherit; z-index: 0; animation: cfab-pulse 1.8s ease-out infinite; }
    .cfab-wa::before { animation-delay: .6s; }
    .cfab-ig::before { background: #D6249F; animation-delay: 1.2s; }
    @keyframes cfab-pulse {
      0%   { transform: scale(1);   opacity: .6; }
      80%  { transform: scale(1.7); opacity: 0; }
      100% { transform: scale(1.7); opacity: 0; }
    }
    @media (prefers-reduced-motion: reduce) { .cfab-btn::before { animation: none; } }
    /* Phones: smaller and lifted clear of the sticky booking bars. */
    @media (max-width: 640px) {
      .cfab { right: 14px; bottom: calc(88px + env(safe-area-inset-bottom)); gap: 12px; }
      .cfab-btn { width: 48px; height: 48px; }
    }
  `,
})
export class ContactFabComponent {
  private readonly contactService = inject(ContactService);
  private readonly contact = this.contactService.get();

  readonly whatsAppLink = computed(() =>
    this.contact()?.whatsAppNumber ? this.contactService.buildWhatsAppLink('Hi Ghumo Odisha! I would like to know more about your trips.') : null,
  );
  readonly telLink = computed(() => {
    const phone = this.contact()?.phone?.replace(/[^\d+]/g, '');
    return phone ? `tel:${phone}` : null;
  });
  /** Admin → Dashboard → Travel Organizer (else appsettings OrganizerContact:InstagramUrl). */
  readonly instagramUrl = computed(() => this.contact()?.instagramUrl || null);
}
