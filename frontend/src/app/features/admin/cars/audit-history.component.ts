import { Component, input } from '@angular/core';
import { CarAuditEvent } from '../../../core/models/admin-car.model';
import { istDateTime } from '../../../shared/utils/car-format';

/** The Cars audit trail: who changed what, when, with the before/after values and reason. Newest first. */
@Component({
  selector: 'app-audit-history',
  standalone: true,
  template: `
    @if (events().length === 0) {
      <p class="note">No history yet.</p>
    } @else {
      <ol class="ach">
        @for (e of events(); track e.carAuditEventId) {
          <li>
            <div class="row sp" style="gap:10px;align-items:baseline">
              <b>{{ e.title }}</b>
              <span class="note" style="white-space:nowrap">{{ when(e.createdAt) }}</span>
            </div>
            <div class="note">by {{ e.actorRole }}{{ e.actorId ? ' #' + e.actorId : '' }}</div>
            @let v = values(e);
            @if (v.old || v.new) {
              <div class="ach-diff">
                @if (v.old) { <span class="ach-old">{{ v.old }}</span> }
                @if (v.old && v.new) { <span class="mut">→</span> }
                @if (v.new) { <span>{{ v.new }}</span> }
              </div>
            }
            @if (e.note) { <div class="note">“{{ e.note }}”</div> }
          </li>
        }
      </ol>
    }
  `,
})
export class AuditHistoryComponent {
  readonly events = input.required<CarAuditEvent[]>();
  readonly when = istDateTime;

  /** Readable before/after: status names in words ("InProgress" → "In progress"), bare ids and unchanged values hidden. */
  values(e: CarAuditEvent): { old: string | null; new: string | null } {
    const tidy = (v: string | null) => {
      if (!v || /^\d+$/.test(v)) return null;
      return /^[A-Z][a-z]+(?:[A-Z][a-z]+)+$/.test(v) ? v.replace(/(?!^)([A-Z])/g, ' $1').replace(/ \w/g, (m) => m.toLowerCase()) : v;
    };
    const oldValue = tidy(e.oldValue);
    const newValue = tidy(e.newValue);
    return oldValue === newValue ? { old: null, new: null } : { old: oldValue, new: newValue };
  }
}
