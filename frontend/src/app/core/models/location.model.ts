export interface GeoPoint {
  latitude: number;
  longitude: number;
}

/** A place picked on the map, as the API takes it. */
export interface TripPlace extends GeoPoint {
  label: string;
}

/** Where cars pick customers up from: a drawn zone and/or a list of PIN codes. */
export interface ServiceArea {
  serviceAreaId: number;
  name: string;
  isActive: boolean;
  boundary: GeoPoint[];
  pincodes: string[];
  createdAt: string;
  updatedAt: string;
}

export interface SaveServiceAreaRequest {
  name: string;
  isActive: boolean;
  boundary: GeoPoint[];
  pincodes: string[];
}

export interface PickupCheck {
  isServiceable: boolean;
  areaName: string | null;
  message: string | null;
}

/** An active pickup zone as the customer's map shows it. */
export interface ServiceZone {
  name: string;
  boundary: GeoPoint[];
}
