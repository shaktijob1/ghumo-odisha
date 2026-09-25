import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { BookingEvent } from '../../core/models/booking.model';
import { bookingEventTone } from '../../core/models/enums.model';

/**
 * Vertical order-tracking timeline (oldest step at the top, like a parcel tracker). Used by the
 * admin booking page and by the customer's My Bookings.
 */
@Component({
  selector: 'app-booking-timeline',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (events.length === 0) {
      <p class="note">No history recorded yet.</p>
    } @else {
      <ol class="tl">
        @for (e of events; track $index; let last = $last) {
          <li class="tl-step" [class.last]="last">
            <span class="tl-dot" [attr.data-tone]="tone(e)" aria-hidden="true"></span>
            <div class="tl-body">
              <div class="tl-head">
                <b>{{ e.title }}</b>
                <span class="tl-when">{{ e.createdAt | date: 'd MMM y, h:mm a' }}</span>
              </div>
              @if (e.description) {
                <p class="tl-desc">{{ e.description }}</p>
              }
              @if (showActor) {
                <span class="tl-actor">by {{ e.actor }}</span>
              }
            </div>
          </li>
        }
      </ol>
    }
  `,
  styles: [
    `
      .tl { list-style: none; margin: 0; padding: 0; }
      .tl-step { position: relative; display: flex; gap: 14px; padding-bottom: 18px; }
      .tl-step::before {
        content: ''; position: absolute; left: 6px; top: 16px; bottom: 0; width: 2px; background: var(--line);
      }
      .tl-step.last { padding-bottom: 0; }
      .tl-step.last::before { display: none; }
      .tl-dot {
        position: relative; flex: 0 0 14px; width: 14px; height: 14px; margin-top: 2px; border-radius: 999px;
        background: #fff; border: 3px solid var(--info);
      }
      .tl-dot[data-tone='ok'] { border-color: var(--ok); background: var(--accent-soft); }
      .tl-dot[data-tone='wait'] { border-color: var(--wait); }
      .tl-dot[data-tone='bad'] { border-color: var(--danger); }
      .tl-body { flex: 1; min-width: 0; }
      .tl-head { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 2px 12px; font-size: 13.5px; }
      .tl-when { color: var(--muted); font-size: 12px; white-space: nowrap; }
      .tl-desc { margin: 3px 0 0; color: var(--muted); font-size: 12.5px; line-height: 1.5; }
      .tl-actor { display: inline-block; margin-top: 3px; font-size: 11px; color: var(--muted); }
    `,
  ],
})
export class BookingTimelineComponent {
  @Input({ required: true }) events: BookingEvent[] = [];
  /** Admin view shows who did each step; customers don't need it. */
  @Input() showActor = false;

  tone(e: BookingEvent): string {
    return bookingEventTone(e.eventType);
  }
}
