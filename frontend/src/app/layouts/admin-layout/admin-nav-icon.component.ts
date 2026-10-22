import { Component, Input } from '@angular/core';

export type AdminNavIcon =
  | 'dashboard' | 'trips' | 'destinations' | 'stories' | 'bookings' | 'refunds' | 'collections'
  | 'customers' | 'coupons' | 'logs' | 'crm' | 'followups' | 'campaigns' | 'pipeline' | 'whatsapp'
  | 'history' | 'reports' | 'car' | 'driver' | 'pin' | 'holidays';

/** Line icons for the admin menu (24×24, stroke = currentColor). */
@Component({
  selector: 'app-admin-nav-icon',
  standalone: true,
  template: `
    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
      @switch (name) {
        @case ('dashboard') { <rect x="3" y="3" width="7" height="9" rx="1.5"></rect><rect x="14" y="3" width="7" height="5" rx="1.5"></rect><rect x="14" y="12" width="7" height="9" rx="1.5"></rect><rect x="3" y="16" width="7" height="5" rx="1.5"></rect> }
        @case ('trips') { <path d="M3 20h18"></path><path d="m5 20 5-9 3 5 2-3 4 7"></path><circle cx="16" cy="6" r="2"></circle> }
        @case ('destinations') { <path d="M12 21s-6.5-5.9-6.5-11A6.5 6.5 0 0 1 18.5 10c0 5.1-6.5 11-6.5 11z"></path><circle cx="12" cy="10" r="2.3"></circle> }
        @case ('stories') { <path d="M4 19.5V5a2 2 0 0 1 2-2h12v16H6a2 2 0 0 0-2 2z"></path><path d="M8 7h6M8 11h6"></path> }
        @case ('bookings') { <rect x="3" y="4" width="18" height="17" rx="2"></rect><path d="M16 2v4M8 2v4M3 10h18M8 15l2.5 2.5L16 13"></path> }
        @case ('refunds') { <path d="M3 12a9 9 0 1 0 3-6.7L3 8"></path><path d="M3 3v5h5"></path><path d="M12 8v8M9.5 10.5h3.75a1.75 1.75 0 0 1 0 3.5H10"></path> }
        @case ('collections') { <rect x="2" y="6" width="20" height="13" rx="2"></rect><circle cx="12" cy="12.5" r="2.5"></circle><path d="M6 10v5M18 10v5"></path> }
        @case ('customers') { <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path><circle cx="9" cy="7" r="4"></circle><path d="M23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"></path> }
        @case ('coupons') { <path d="M3 9V6a1 1 0 0 1 1-1h16a1 1 0 0 1 1 1v3a3 3 0 0 0 0 6v3a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1v-3a3 3 0 0 0 0-6z"></path><path d="M10 9.5h.01M14 14.5h.01M14.5 9l-5 6"></path> }
        @case ('logs') { <path d="M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z"></path><path d="M14 3v6h6M8 13h8M8 17h5"></path> }
        @case ('crm') { <circle cx="12" cy="8" r="4"></circle><path d="M4 21a8 8 0 0 1 16 0"></path> }
        @case ('followups') { <circle cx="12" cy="12" r="9"></circle><path d="M12 7v5l3 2"></path> }
        @case ('campaigns') { <path d="m3 11 15-6v14L3 13z"></path><path d="M7 12.5V18a2 2 0 0 0 4 0v-3.5"></path> }
        @case ('pipeline') { <rect x="3" y="4" width="5" height="16" rx="1.5"></rect><rect x="10" y="4" width="5" height="11" rx="1.5"></rect><rect x="17" y="4" width="4" height="7" rx="1.5"></rect> }
        @case ('whatsapp') { <path d="M21 11.5a8.4 8.4 0 0 1-12.4 7.4L3 20.5l1.6-5.4A8.5 8.5 0 1 1 21 11.5z"></path> }
        @case ('history') { <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"></path><path d="M8 9h8M8 13h5"></path> }
        @case ('reports') { <path d="M3 3v18h18"></path><path d="M7 15l4-4 3 3 5-6"></path> }
        @case ('car') { <path d="M5 17H3v-5l2-5h12l3 5h1v5h-2"></path><circle cx="7.5" cy="17.5" r="2"></circle><circle cx="16.5" cy="17.5" r="2"></circle><path d="M9.5 17.5h5"></path> }
        @case ('driver') { <circle cx="12" cy="12" r="9"></circle><circle cx="12" cy="12" r="2.5"></circle><path d="M12 3v6.5M4.2 16.5l5.6-3.2M19.8 16.5l-5.6-3.2"></path> }
        @case ('holidays') { <path d="M12 10v11"></path><path d="M12 10C10 6 6 5.5 3.5 7c2.5.2 4.5 1.3 5.5 3"></path><path d="M12 10c2-4 6-4.5 8.5-3-2.5.2-4.5 1.3-5.5 3"></path><path d="M4 21c2.5-2 5-2.5 8-2.5s5.5.5 8 2.5"></path> }
        @case ('pin') { <circle cx="12" cy="10" r="3"></circle><path d="M12 21s-7-6.2-7-11a7 7 0 0 1 14 0c0 4.8-7 11-7 11z"></path> }
      }
    </svg>
  `,
  styles: `:host { display: inline-grid; place-items: center; }`,
})
export class AdminNavIconComponent {
  @Input({ required: true }) name!: AdminNavIcon;
}
