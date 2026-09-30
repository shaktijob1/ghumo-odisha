import { CarWindow } from '../../core/models/car.model';
import { TripPlace } from '../../core/models/location.model';
import { windowFromParams, windowToParams } from './car-format';

/**
 * A vehicle search: when, where the customer is picked up, where they're going ("drop" in the API) and
 * whether it's one way or a round trip back to the pickup. It lives in the
 * URL so results and the booking page share it and survive a refresh:
 *   ?date=2026-10-02&time=10:00&hours=12&from=20.27,85.84&fromName=…&drop=19.81,85.83&dropName=…[&trip=round]
 */
export interface VehicleSearch {
  window: CarWindow;
  pickup: TripPlace;
  drop: TripPlace;
  roundTrip: boolean;
}

export function searchToParams(s: VehicleSearch): Record<string, string | number> {
  return windowToParams(s.window, {
    from: point(s.pickup),
    fromName: s.pickup.label,
    drop: point(s.drop),
    dropName: s.drop.label,
    trip: s.roundTrip ? 'round' : null,
  });
}

export function searchFromParams(params: { get(name: string): string | null }): VehicleSearch | null {
  const window = windowFromParams(params);
  const pickup = place(params.get('from'), params.get('fromName'));
  const drop = place(params.get('drop'), params.get('dropName'));
  if (!window || !pickup || !drop) return null;
  return { window, pickup, drop, roundTrip: params.get('trip') === 'round' };
}

/** The route part of a quote / fare-search / booking request body. */
export function routeBody(s: VehicleSearch) {
  return {
    pickupDate: s.window.date,
    pickupTime: s.window.time,
    durationHours: s.window.hours,
    pickup: s.pickup,
    drop: s.drop,
    roundTrip: s.roundTrip,
  };
}

/** "Master Canteen Area" from "Master Canteen Area, Kharvela Nagar, Bhubaneswar, Odisha 751001, India". */
export function shortPlace(label: string): string {
  return label.split(',')[0].trim();
}

function point(p: TripPlace): string {
  return `${p.latitude.toFixed(6)},${p.longitude.toFixed(6)}`;
}

function place(value: string | null, name: string | null): TripPlace | null {
  const m = value?.match(/^(-?\d+(?:\.\d+)?),(-?\d+(?:\.\d+)?)$/);
  if (!m || !name?.trim()) return null;
  const latitude = Number(m[1]);
  const longitude = Number(m[2]);
  if (Math.abs(latitude) > 90 || Math.abs(longitude) > 180) return null;
  return { latitude, longitude, label: name.trim() };
}
