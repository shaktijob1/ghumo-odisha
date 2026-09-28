/**
 * Arrow-button scroll for a horizontal scroll-snap row.
 *
 * iOS Safari can let a programmatic smooth scroll settle on a card's snap point that lies past the
 * row's real end (the last cards' "start" edges can't all reach the left side), leaving the row
 * scrolled into blank space. Finger swipes clamp correctly, so only the arrows hit it. Here the
 * target is picked from card positions and clamped to the scrollable range ourselves, with
 * snapping paused until the scroll settles.
 */
export function scrollRowBy(el: HTMLElement, delta: number, cardSelector: string, smooth = true): void {
  const max = Math.max(0, el.scrollWidth - el.clientWidth);
  const raw = Math.min(max, Math.max(0, el.scrollLeft + delta));

  const padStart = parseFloat(getComputedStyle(el).scrollPaddingInlineStart) || 0;
  const rowLeft = el.getBoundingClientRect().left;
  const points = Array.from(el.querySelectorAll<HTMLElement>(cardSelector))
    .map((c) => Math.min(max, Math.max(0, c.getBoundingClientRect().left - rowLeft + el.scrollLeft - padStart)));
  points.push(0, max);
  // Only points in the direction of travel, so a click always moves at least one card.
  const ahead = points.filter((p) => (delta > 0 ? p > el.scrollLeft + 1 : p < el.scrollLeft - 1));
  if (ahead.length === 0) return;
  const target = Math.round(ahead.reduce((best, p) => (Math.abs(p - raw) < Math.abs(best - raw) ? p : best)));

  el.style.scrollSnapType = 'none';
  let settle: ReturnType<typeof setTimeout>;
  const restore = () => {
    clearTimeout(settle);
    clearTimeout(cap);
    el.removeEventListener('scroll', onScroll);
    el.style.scrollSnapType = '';
  };
  // No reliable `scrollend` on older iOS: restore once scroll events stop for a moment.
  const onScroll = () => {
    clearTimeout(settle);
    settle = setTimeout(restore, 150);
  };
  const cap = setTimeout(restore, 1500);
  el.addEventListener('scroll', onScroll, { passive: true });
  settle = setTimeout(restore, 300);
  el.scrollTo({ left: target, behavior: smooth ? 'smooth' : 'auto' });
}
