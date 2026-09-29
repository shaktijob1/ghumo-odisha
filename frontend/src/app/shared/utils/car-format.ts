import { CarWindow } from '../../core/models/car.model';

// Cars are booked and driven in Odisha: every date/time on the car screens is shown in India time,
// whatever the visitor's device is set to.
const IST = 'Asia/Kolkata';

/** "Fri, 2 Oct 2026" */
export function istDate(iso: string): string {
  const d = new Date(iso);
  const weekday = d.toLocaleDateString('en-IN', { weekday: 'short', timeZone: IST });
  const dayMonth = d.toLocaleDateString('en-IN', { day: 'numeric', month: 'short', timeZone: IST }).replace('Sept', 'Sep');
  const year = d.toLocaleDateString('en-IN', { year: 'numeric', timeZone: IST });
  return `${weekday}, ${dayMonth} ${year}`;
}

/** "10:00 AM" */
export function istTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('en-IN', { hour: 'numeric', minute: '2-digit', hour12: true, timeZone: IST }).toUpperCase();
}

/** "2 Oct 2026 · 10:05 AM" */
export function istDateTime(iso: string): string {
  const d = new Date(iso);
  const day = d.toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric', timeZone: IST }).replace('Sept', 'Sep');
  return `${day} · ${istTime(iso)}`;
}

/** "yyyy-MM-dd" → "Fri, 2 Oct 2026" without time-zone drift. */
export function windowDateLabel(date: string): string {
  return istDate(`${date}T12:00:00+05:30`);
}

/** "14:30" → "2:30 PM" */
export function timeLabel(time: string): string {
  const [h, m] = time.split(':').map(Number);
  const suffix = h >= 12 ? 'PM' : 'AM';
  const hour12 = h % 12 === 0 ? 12 : h % 12;
  return `${hour12}:${String(m).padStart(2, '0')} ${suffix}`;
}

/** 12 → "12 Hours", 24 → "24 Hours", 48 → "2 Days", 30 → "30 Hours" */
export function durationLabel(hours: number): string {
  if (hours > 24 && hours % 24 === 0) return `${hours / 24} Days`;
  return `${hours} Hours`;
}

/** 30-minute pickup time slots, "00:00" … "23:30". */
export const PICKUP_TIMES: { value: string; label: string }[] = Array.from({ length: 48 }, (_, i) => {
  const value = `${String(Math.floor(i / 2)).padStart(2, '0')}:${i % 2 ? '30' : '00'}`;
  return { value, label: timeLabel(value) };
});

/** Duration choices on the search forms. "custom" lets the customer type hours/days. */
export const DURATION_PRESETS: { hours: number; label: string }[] = [
  { hours: 12, label: '12 Hours' },
  { hours: 24, label: '24 Hours' },
  { hours: 48, label: '2 Days' },
  { hours: 72, label: '3 Days' },
];

/** "yyyy-MM-dd" for the India calendar day `offsetDays` from today. */
export function istDateValue(offsetDays = 0): string {
  const now = new Date(Date.now() + offsetDays * 86_400_000);
  return now.toLocaleDateString('en-CA', { timeZone: IST });
}

/** Search/booking window ⇄ URL query params (?date=…&time=…&hours=…), so the choice survives navigation. */
export function windowFromParams(params: { get(name: string): string | null }): CarWindow | null {
  const date = params.get('date');
  const time = params.get('time');
  const hours = Number(params.get('hours'));
  if (!date || !/^\d{4}-\d{2}-\d{2}$/.test(date) || !time || !/^\d{2}:\d{2}$/.test(time) || !Number.isInteger(hours) || hours <= 0) {
    return null;
  }
  return { date, time, hours };
}

export function windowToParams(window: CarWindow | null, extra: Record<string, string | number | null | undefined> = {}): Record<string, string | number> {
  const params: Record<string, string | number> = {};
  if (window) {
    params['date'] = window.date;
    params['time'] = window.time;
    params['hours'] = window.hours;
  }
  for (const [k, v] of Object.entries(extra)) {
    if (v !== null && v !== undefined && v !== '') params[k] = v;
  }
  return params;
}

/** Default window for a first visit: tomorrow, 10:00 AM, 12 hours. */
export function defaultWindow(): CarWindow {
  return { date: istDateValue(1), time: '10:00', hours: 12 };
}
