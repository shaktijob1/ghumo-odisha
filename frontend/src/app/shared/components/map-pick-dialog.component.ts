import { Component, OnInit, inject, input, output, signal } from '@angular/core';
import { ServiceZone } from '../../core/models/location.model';
import { CarService } from '../../core/services/car.service';
import { PickedPlace } from '../../core/services/google-maps.service';
import { LocationPickerComponent } from './location-picker.component';

/**
 * "Choose on map": a dialog with a map (search, current location, tap or drag the pin). With
 * showZones, the pickup areas are shaded so the customer can see where vehicles pick up.
 */
@Component({
  selector: 'app-map-pick-dialog',
  standalone: true,
  imports: [LocationPickerComponent],
  template: `
    <div class="modal-backdrop" (click)="closed.emit()">
      <div class="modal mpd" role="dialog" aria-modal="true" [attr.aria-label]="title()" (click)="$event.stopPropagation()">
        <button type="button" class="mpd-x" aria-label="Close" (click)="closed.emit()">×</button>
        <h4>{{ title() }}</h4>
        @if (showZones() && zones().length) { <p class="note mpd-hint">Shaded areas are where our vehicles pick up.</p> }
        @if (ready()) {
          <app-location-picker inputId="mpd-search" placeholder="Search area, hotel or landmark" [initial]="initial()" [zones]="zones()" (changed)="place.set($event)"></app-location-picker>
        }
        @if (place(); as p) { <p class="mpd-sel"><b>Selected:</b> {{ p.label }}</p> }
        <div class="mpd-btns">
          <button type="button" class="btn ghost" (click)="closed.emit()">Cancel</button>
          <button type="button" class="btn" [disabled]="!place()" (click)="picked.emit(place()!)">Use this location</button>
        </div>
      </div>
    </div>
  `,
  styles: `
    .mpd { max-width: 640px; padding: 22px; }
    .mpd h4 { font-size: 17px; margin: 0 30px 6px 0; }
    .mpd-hint { margin-bottom: 10px; }
    .mpd-x { position: absolute; top: 14px; right: 14px; width: 32px; height: 32px; border-radius: 50%; border: 1px solid var(--line); background: #fff; font-size: 18px; line-height: 1; cursor: pointer; color: var(--ink); }
    .mpd-sel { margin-top: 10px; font-size: 13px; }
    .mpd-sel b { font-weight: 600; }
    .mpd-btns { display: flex; gap: 10px; justify-content: flex-end; margin-top: 16px; }
    @media (max-width: 640px) {
      .mpd { padding: 18px 14px; }
      .mpd-btns .btn { flex: 1; }
    }
  `,
})
export class MapPickDialogComponent implements OnInit {
  private readonly cars = inject(CarService);

  readonly title = input('Choose on map');
  readonly initial = input<PickedPlace | null>(null);
  readonly showZones = input(false);
  readonly picked = output<PickedPlace>();
  readonly closed = output<void>();

  readonly place = signal<PickedPlace | null>(null);
  readonly zones = signal<ServiceZone[]>([]);
  /** The map is created once the zones are known, so it can fit them in view. */
  readonly ready = signal(false);

  ngOnInit(): void {
    this.place.set(this.initial());
    if (!this.showZones()) {
      this.ready.set(true);
      return;
    }
    this.cars.serviceZones().subscribe({
      next: (z) => {
        this.zones.set(z);
        this.ready.set(true);
      },
      error: () => this.ready.set(true),
    });
  }
}
