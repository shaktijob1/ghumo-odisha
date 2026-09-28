/**
 * Readable trip URLs: /trips/1-puri-konark-satapada. Only the leading number is looked up, so a
 * renamed trip's old links keep working. Mirrors SeoSlug on the API — keep the two in sync.
 */
export function slugify(text: string): string {
  return text.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');
}

export function tripPath(tripId: number, title: string): string {
  const slug = slugify(title);
  return slug ? `/trips/${tripId}-${slug}` : `/trips/${tripId}`;
}

/** The trip id at the start of a /trips/:id segment ("12-puri-trip" → 12, "12" → 12). */
export function parseTripId(segment: string | null): number {
  return parseInt(segment ?? '', 10);
}

/** "2 Days / 1 Night" for a departure — same wording as the API's trip summaries. */
export function durationLabel(startDate: string, endDate: string): string {
  const toDay = (d: string) => Date.UTC(+d.slice(0, 4), +d.slice(5, 7) - 1, +d.slice(8, 10)) / 86_400_000;
  const days = toDay(endDate) - toDay(startDate) + 1;
  const nights = days - 1;
  return `${days} ${days === 1 ? 'Day' : 'Days'} / ${nights} ${nights === 1 ? 'Night' : 'Nights'}`;
}
