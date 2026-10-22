import { Component, EventEmitter, Input, Output, inject, signal } from '@angular/core';
import { TripBookingState } from '../../../core/models/enums.model';
import { AdminTripService } from '../../../core/services/admin-trip.service';
import { ToastService } from '../../../core/services/toast.service';

/**
 * Open / Booked / Closed for a whole trip, saved the moment it's clicked. Booked shows every date
 * as sold out and Closed as "Seats filled"; neither takes online bookings, but Bookings → Add
 * booking still works for phone customers.
 */
@Component({
  selector: 'app-booking-state-switch',
  standalone: true,
  template: `
    <div class="bss" role="radiogroup" aria-label="Online bookings" [class.compact]="compact">
      @for (o of options; track o.value) {
        <button type="button" role="radio" class="bss-opt" [attr.data-k]="o.cls" [class.on]="state() === o.value"
          [attr.aria-checked]="state() === o.value" [disabled]="saving()" [title]="o.hint" (click)="choose(o.value)">
          <span class="bss-dot" aria-hidden="true"></span>{{ o.label }}
        </button>
      }
    </div>
  `,
  styles: `
    .bss { display: inline-flex; gap: 4px; padding: 3px; border-radius: 999px; background: var(--canvas); border: 1px solid var(--line); }
    .bss-opt { display: inline-flex; align-items: center; gap: 6px; padding: 6px 12px; border: 0; border-radius: 999px; background: transparent; color: var(--muted); font: inherit; font-size: 12px; font-weight: 600; cursor: pointer; white-space: nowrap; transition: background .15s, color .15s; }
    .bss.compact .bss-opt { padding: 4px 9px; font-size: 11.5px; }
    .bss-opt:disabled { cursor: progress; }
    .bss-dot { width: 7px; height: 7px; border-radius: 50%; background: currentColor; opacity: .45; }
    .bss-opt.on .bss-dot { opacity: 1; }
    .bss-opt[data-k=open].on { background: #DDF3EA; color: #0F6F5C; }
    .bss-opt[data-k=booked].on { background: #FBE3DF; color: #B23A2E; }
    .bss-opt[data-k=closed].on { background: #FDF1DC; color: #9A6A11; }
    @media (pointer: coarse) { .bss-opt { padding: 9px 14px; } }
  `,
})
export class BookingStateSwitchComponent {
  private readonly tripService = inject(AdminTripService);
  private readonly toast = inject(ToastService);

  @Input({ required: true }) tripId!: number;
  @Input() compact = false;
  @Input() set value(v: TripBookingState) {
    this.state.set(v ?? TripBookingState.Open);
  }
  @Output() changed = new EventEmitter<TripBookingState>();

  readonly state = signal(TripBookingState.Open);
  readonly saving = signal(false);

  readonly options = [
    { value: TripBookingState.Open, label: 'Open', cls: 'open', hint: 'Customers can book online.' },
    { value: TripBookingState.Booked, label: 'Booked', cls: 'booked', hint: 'Fully booked: every date shows as sold out.' },
    { value: TripBookingState.Closed, label: 'Closed', cls: 'closed', hint: 'Bookings closed: every date shows "Seats filled".' },
  ];

  choose(next: TripBookingState): void {
    if (next === this.state() || this.saving()) return;
    const previous = this.state();
    this.state.set(next);
    this.saving.set(true);
    this.tripService.setBookingState(this.tripId, next).subscribe({
      next: (message) => {
        this.saving.set(false);
        this.toast.success(message);
        this.changed.emit(next);
      },
      error: () => {
        this.saving.set(false);
        this.state.set(previous);
      },
    });
  }
}
