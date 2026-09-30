import { GeoPoint } from '../../core/models/location.model';

/** Opens Google Maps directions to a point (the Maps app on phones). */
export function directionsLink(point: GeoPoint): string {
  return `https://www.google.com/maps/dir/?api=1&destination=${point.latitude},${point.longitude}`;
}

/** How a booking's km were made up, e.g. "3 km to pickup + 61 km there + 61 km back + 3 km home" (null for older bookings). */
export function kmBreakdown(b: {
  roundTrip: boolean;
  driverApproachKm: number | null;
  pickupToDropKm: number | null;
  dropToPickupKm: number | null;
  returnToBaseKm: number | null;
}): string | null {
  if (b.pickupToDropKm === null) return null;
  const back = b.roundTrip ? ` + ${b.dropToPickupKm ?? 0} km back to pickup` : '';
  return `${b.driverApproachKm ?? 0} km to pickup + ${b.pickupToDropKm} km there${back} + ${b.returnToBaseKm ?? 0} km home`;
}
