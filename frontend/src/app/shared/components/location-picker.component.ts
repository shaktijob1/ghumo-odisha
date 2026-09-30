import { AfterViewInit, Component, ElementRef, OnDestroy, inject, input, output, signal, viewChild } from '@angular/core';
import { DEFAULT_MAP_CENTER, GoogleMapsService, PickedPlace } from '../../core/services/google-maps.service';
import { PlaceSearchComponent } from './place-search.component';
import { ServiceZone } from '../../core/models/location.model';

/**
 * Pick one point: search a place, use the current location, click the map or drag the pin. Emits the
 * point with a readable address each time it changes.
 */
@Component({
  selector: 'app-location-picker',
  standalone: true,
  imports: [PlaceSearchComponent],
  template: `
    @if (showSearch()) {
    <app-place-search
      [inputId]="inputId()"
      [placeholder]="placeholder()"
      [allowCurrentLocation]="true"
      [value]="label()"
      (picked)="setPlace($event, true)"
    ></app-place-search>
    }
    <div class="lp-map" #mapEl>
      @if (mapError()) { <div class="lp-err">{{ mapError() }}</div> }
    </div>
    <div class="note" style="margin-top:6px">Search, or tap the map / drag the pin to the exact spot.</div>
  `,
  styles: `
    .lp-map { position: relative; height: 280px; margin-top: 10px; border: 1px solid var(--line); border-radius: var(--radius-control); overflow: hidden; background: var(--canvas); }
    .lp-err { position: absolute; inset: 0; display: grid; place-items: center; padding: 16px; text-align: center; font-size: 12.5px; color: var(--muted); }
  `,
})
export class LocationPickerComponent implements AfterViewInit, OnDestroy {
  private readonly maps = inject(GoogleMapsService);
  private readonly mapEl = viewChild.required<ElementRef<HTMLDivElement>>('mapEl');

  readonly inputId = input('location-picker');
  readonly placeholder = input('Search for a place');
  readonly initial = input<PickedPlace | null>(null);
  /** Allowed pickup zones, shaded on the map (display only — the server does the real check). */
  readonly zones = input<ServiceZone[]>([]);
  readonly showSearch = input(true);
  readonly changed = output<PickedPlace>();

  readonly label = signal<string | null>(null);
  readonly mapError = signal<string | null>(null);

  private map?: google.maps.Map;
  private marker?: google.maps.marker.AdvancedMarkerElement;
  private listeners: google.maps.MapsEventListener[] = [];

  async ngAfterViewInit(): Promise<void> {
    const start = this.initial();
    if (start) this.label.set(start.label);
    try {
      await this.maps.load();
      const { Map } = (await google.maps.importLibrary('maps')) as google.maps.MapsLibrary;
      const { AdvancedMarkerElement } = (await google.maps.importLibrary('marker')) as google.maps.MarkerLibrary;
      const center = start ? { lat: start.latitude, lng: start.longitude } : DEFAULT_MAP_CENTER;
      this.map = new Map(this.mapEl().nativeElement, {
        center,
        zoom: start ? 15 : 12,
        mapId: this.maps.mapId,
        streetViewControl: false,
        mapTypeControl: false,
        fullscreenControl: false,
        clickableIcons: false,
        gestureHandling: 'cooperative',
      });
      const zones = this.zones();
      if (zones.length) {
        const bounds = new google.maps.LatLngBounds();
        for (const z of zones) {
          const path = z.boundary.map((p) => ({ lat: p.latitude, lng: p.longitude }));
          path.forEach((p) => bounds.extend(p));
          // Not clickable, so a tap inside a zone still reaches the map and moves the pin.
          new google.maps.Polygon({ map: this.map, paths: path, clickable: false, strokeColor: '#0F6F5C', strokeWeight: 2, strokeOpacity: 0.8, fillColor: '#0F6F5C', fillOpacity: 0.08 });
        }
        if (!start) this.map.fitBounds(bounds, 30);
      }
      this.marker = new AdvancedMarkerElement({ map: this.map, position: start ? center : null, gmpDraggable: true, title: 'Drag to adjust' });
      this.listeners.push(
        this.marker.addListener('dragend', () => {
          const p = this.marker!.position as google.maps.LatLngLiteral | google.maps.LatLng | null;
          if (p) void this.fromPoint(typeof p.lat === 'function' ? (p as google.maps.LatLng).toJSON() : (p as google.maps.LatLngLiteral));
        }),
        this.map.addListener('click', (e: google.maps.MapMouseEvent) => {
          if (e.latLng) void this.fromPoint(e.latLng.toJSON());
        }),
      );
    } catch (e) {
      this.mapError.set(e instanceof Error ? e.message : 'Could not load the map.');
    }
  }

  setPlace(place: PickedPlace, recenter: boolean): void {
    this.label.set(place.label);
    const pos = { lat: place.latitude, lng: place.longitude };
    if (this.marker) this.marker.position = pos;
    if (recenter && this.map) {
      this.map.panTo(pos);
      this.map.setZoom(16);
    }
    this.changed.emit(place);
  }

  private async fromPoint(p: google.maps.LatLngLiteral): Promise<void> {
    if (this.marker) this.marker.position = p;
    const label = await this.maps.addressAt(p.lat, p.lng);
    this.setPlace({ latitude: p.lat, longitude: p.lng, label }, false);
  }

  ngOnDestroy(): void {
    this.listeners.forEach((l) => l.remove());
  }
}
