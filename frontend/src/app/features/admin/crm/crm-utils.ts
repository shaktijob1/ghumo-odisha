import { toLocalDateKey } from '../../../shared/utils/date-key';

/** "YYYY-MM-DD" for today (the admin's local calendar, i.e. India). */
export function todayKey(): string {
  return toLocalDateKey(new Date());
}

/** "YYYY-MM-DD" n days from today. */
export function daysFromToday(n: number): string {
  const d = new Date();
  d.setDate(d.getDate() + n);
  return toLocalDateKey(d);
}

/** Whether a follow-up date ("YYYY-MM-DD") is past, today or later — for colouring it. */
export function dueState(date: string | null | undefined): 'overdue' | 'today' | 'later' | null {
  if (!date) return null;
  const key = date.slice(0, 10);
  const today = todayKey();
  return key < today ? 'overdue' : key === today ? 'today' : 'later';
}

/** Mobile as shown in the CRM: "98765 43210". */
export function formatPhone(phone: string | null | undefined): string {
  if (!phone) return '';
  return phone.length === 10 ? `${phone.slice(0, 5)} ${phone.slice(5)}` : phone;
}
