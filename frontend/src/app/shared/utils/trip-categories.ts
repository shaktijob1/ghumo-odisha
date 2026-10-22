/**
 * Trip categories, in the order the home page filter and the admin trip form show them. Keys match
 * the API's TripCategory names exactly ("PoolParty"); everything else here is display only.
 */
export interface TripCategoryOption {
  key: string;
  label: string;
  emoji: string;
  /** Chip colours: soft background, strong foreground. */
  bg: string;
  fg: string;
}

export const TRIP_CATEGORIES: readonly TripCategoryOption[] = [
  { key: 'PoolParty', label: 'Pool Party', emoji: '🏊', bg: '#E3F2FD', fg: '#1565C0' },
  { key: 'Adventure', label: 'Adventure', emoji: '🧗', bg: '#FFF0E6', fg: '#D9541E' },
  { key: 'Trekking', label: 'Trekking', emoji: '🥾', bg: '#EAF5E4', fg: '#3D7A22' },
  { key: 'Beach', label: 'Beach', emoji: '🏖️', bg: '#FFF7DB', fg: '#A77A00' },
  { key: 'Camping', label: 'Camping', emoji: '⛺', bg: '#F1ECFB', fg: '#6A43B5' },
  { key: 'Waterfalls', label: 'Waterfalls', emoji: '💦', bg: '#E2F6F6', fg: '#0E7C86' },
  { key: 'Heritage', label: 'Heritage', emoji: '🛕', bg: '#FCEAEA', fg: '#B23A48' },
  { key: 'Hills', label: 'Hills', emoji: '⛰️', bg: '#E6F2EE', fg: '#0F6F5C' },
];

export function tripCategory(key: string): TripCategoryOption | undefined {
  return TRIP_CATEGORIES.find((c) => c.key === key);
}
