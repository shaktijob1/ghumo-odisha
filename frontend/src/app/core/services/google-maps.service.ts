import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { FeatureService } from './feature.service';

/** A place picked on the map: a point plus the name / address shown to people. */
export interface PickedPlace {
  latitude: number;
  longitude: number;
  label: string;
}

export interface PlaceSuggestion {
  mainText: string;
  secondaryText: string;
  prediction: google.maps.places.PlacePrediction;
}

/** Bhubaneswar — where maps open and where suggestions are biased towards. */
export const DEFAULT_MAP_CENTER: google.maps.LatLngLiteral = { lat: 20.2961, lng: 85.8245 };

/**
 * Loads the Google Maps JavaScript API once (key from /api/features — never hardcoded here) and wraps the
 * few calls the app needs: place suggestions (Places API New), place details, reverse geocoding and the
 * browser's current position. Distances for fares are NOT measured here — the API does that itself.
 */
@Injectable({ providedIn: 'root' })
export class GoogleMapsService {
  private readonly features = inject(FeatureService);
  private loading?: Promise<void>;
  private mapIdValue = 'DEMO_MAP_ID';
  private geocoder?: google.maps.Geocoder;

  get mapId(): string {
    return this.mapIdValue;
  }

  /** Resolves once google.maps is ready; rejects with a readable message if Maps isn't set up or can't load. */
  load(): Promise<void> {
    this.loading ??= this.inject().catch((e) => {
      this.loading = undefined; // allow a retry after a network blip
      throw e;
    });
    return this.loading;
  }

  private async inject(): Promise<void> {
    if ((window as { google?: { maps?: { importLibrary?: unknown } } }).google?.maps?.importLibrary) return;
    const f = await firstValueFrom(this.features.load());
    if (!f.googleMapsApiKey) throw new Error('Maps are not set up yet.');
    this.mapIdValue = f.googleMapsMapId;

    await new Promise<void>((resolve, reject) => {
      const callback = '__ghumoMapsReady';
      (window as unknown as Record<string, unknown>)[callback] = () => resolve();
      const script = document.createElement('script');
      const params = new URLSearchParams({
        key: f.googleMapsApiKey!,
        v: 'weekly',
        loading: 'async',
        libraries: 'places,marker',
        language: 'en',
        region: 'IN',
        callback,
      });
      script.src = `https://maps.googleapis.com/maps/api/js?${params}`;
      script.async = true;
      script.onerror = () => reject(new Error('Could not load Google Maps. Check your connection and try again.'));
      document.head.appendChild(script);
    });
  }

  /** Place suggestions for what the customer is typing, limited to India and biased towards the map centre. */
  async suggest(
    input: string,
    sessionToken: google.maps.places.AutocompleteSessionToken,
    near: google.maps.LatLngLiteral = DEFAULT_MAP_CENTER,
  ): Promise<PlaceSuggestion[]> {
    await this.load();
    const { AutocompleteSuggestion } = (await google.maps.importLibrary('places')) as google.maps.PlacesLibrary;
    const { suggestions } = await AutocompleteSuggestion.fetchAutocompleteSuggestions({
      input,
      sessionToken,
      includedRegionCodes: ['in'],
      locationBias: { center: near, radius: 50000 },
    });
    return suggestions
      .filter((s) => !!s.placePrediction)
      .map((s) => ({
        mainText: s.placePrediction!.mainText?.toString() ?? s.placePrediction!.text.toString(),
        secondaryText: s.placePrediction!.secondaryText?.toString() ?? '',
        prediction: s.placePrediction!,
      }));
  }

  async newSessionToken(): Promise<google.maps.places.AutocompleteSessionToken> {
    await this.load();
    const { AutocompleteSessionToken } = (await google.maps.importLibrary('places')) as google.maps.PlacesLibrary;
    return new AutocompleteSessionToken();
  }

  /** The point and a readable name for a chosen suggestion (ends the billing session). */
  async resolve(suggestion: PlaceSuggestion): Promise<PickedPlace> {
    const place = suggestion.prediction.toPlace();
    await place.fetchFields({ fields: ['location', 'displayName', 'formattedAddress'] });
    if (!place.location) throw new Error('That place has no location. Please pick another.');
    const name = place.displayName ?? suggestion.mainText;
    const address = place.formattedAddress ?? suggestion.secondaryText;
    const label = address && !address.startsWith(name) ? `${name}, ${address}` : address || name;
    return { latitude: place.location.lat(), longitude: place.location.lng(), label };
  }

  /** A readable address for a point (map click, dragged pin, current location). */
  async addressAt(latitude: number, longitude: number): Promise<string> {
    await this.load();
    const { Geocoder } = (await google.maps.importLibrary('geocoding')) as google.maps.GeocodingLibrary;
    this.geocoder ??= new Geocoder();
    try {
      const { results } = await this.geocoder.geocode({ location: { lat: latitude, lng: longitude } });
      return results[0]?.formatted_address ?? `${latitude.toFixed(5)}, ${longitude.toFixed(5)}`;
    } catch {
      return `${latitude.toFixed(5)}, ${longitude.toFixed(5)}`;
    }
  }

  /** The device's current location, with its address. Browsers only allow this on https (or localhost). */
  async currentPlace(): Promise<PickedPlace> {
    if (!('geolocation' in navigator)) throw new Error('This device can’t share its location. Please type the place instead.');
    const position = await new Promise<GeolocationPosition>((resolve, reject) =>
      navigator.geolocation.getCurrentPosition(resolve, reject, { enableHighAccuracy: true, timeout: 15000, maximumAge: 60000 }),
    ).catch((e: GeolocationPositionError) => {
      throw new Error(
        e.code === e.PERMISSION_DENIED
          ? 'Location permission is off. Allow location for this site, or type the place instead.'
          : 'Couldn’t find your location. Please type the place instead.',
      );
    });
    const { latitude, longitude } = position.coords;
    return { latitude, longitude, label: await this.addressAt(latitude, longitude) };
  }
}
