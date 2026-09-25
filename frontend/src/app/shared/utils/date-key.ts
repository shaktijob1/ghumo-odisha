// Slot dates come from the API as plain "yyyy-MM-dd" (DateOnly), so they're compared as strings
// against today's local date — no Date parsing, no timezone shift.
export function toLocalDateKey(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** True when the departure starts today or later. Display-only — the API enforces the same rule. */
export function isUpcomingSlot(slot: { startDate: string }): boolean {
  return slot.startDate.slice(0, 10) >= toLocalDateKey(new Date());
}
