import { Component, input, output, signal } from '@angular/core';
import { PickedPlace } from '../../core/services/google-maps.service';
import { GeoPoint } from '../../core/models/location.model';
import { LocationPickerComponent } from './location-picker.component';

/**
 * A driver's starting point (home / stand). The km from here to each customer's pickup are added to the
 * fare, so it has to be set before the driver's cars can be booked. Saving is done by the parent page.
 */
@Component({
  selector: 'app-base-location-panel',
  standalone: true,
  imports: [LocationPickerComponent],
  template: `
    <div [class]="variant() === 'driver' ? 'card pad' : 'panel'" style="margin-bottom:14px">
      <div class="row" style="justify-content:space-between;gap:10px;flex-wrap:wrap">
        <div>
          @if (variant() === 'driver') { <h3 class="cz-h3">Starting point</h3> } @else { <h4>Starting point</h4> }
          <div class="note">{{ intro() }}</div>
        </div>
        @if (!editing()) {
          <button type="button" class="btn sm ghost" (click)="startEdit()">{{ point() ? 'Change' : 'Set on map' }}</button>
        }
      </div>

      @if (!editing()) {
        @if (point()) {
          <p style="margin-top:10px;font-size:13px"><b style="font-weight:500">{{ label() }}</b></p>
          <a class="note" target="_blank" rel="noopener" [href]="mapsLink()">Open in Google Maps ↗</a>
        } @else {
          <p class="err-msg" style="margin-top:10px">Not set — cars can't be booked online until it is.</p>
        }
      } @else {
        <div style="margin-top:12px">
          <app-location-picker
            inputId="base-location"
            placeholder="Search the area, street or landmark"
            [initial]="initialPlace()"
            (changed)="draft.set($event)"
          ></app-location-picker>
        </div>
        @if (error()) { <div class="err-msg">{{ error() }}</div> }
        <div class="row" style="gap:8px;margin-top:12px">
          <button type="button" class="btn sm" [disabled]="!draft() || saving()" (click)="draft() && save.emit(draft()!)">
            @if (saving()) { <span class="spin"></span> } @else { Save starting point }
          </button>
          <button type="button" class="btn sm ghost" [disabled]="saving()" (click)="editing.set(false)">Cancel</button>
        </div>
      }
    </div>
  `,
})
export class BaseLocationPanelComponent {
  readonly variant = input<'admin' | 'driver'>('admin');
  readonly point = input<GeoPoint | null>(null);
  readonly label = input<string | null>(null);
  readonly saving = input(false);
  readonly error = input<string | null>(null);
  readonly intro = input('Where the driver starts from. The km from here to the customer’s pickup are added to the fare.');
  readonly save = output<PickedPlace>();

  readonly editing = signal(false);
  readonly draft = signal<PickedPlace | null>(null);

  /** Parent calls this after a successful save. */
  done(): void {
    this.editing.set(false);
    this.draft.set(null);
  }

  startEdit(): void {
    this.draft.set(null);
    this.editing.set(true);
  }

  initialPlace(): PickedPlace | null {
    const p = this.point();
    return p ? { latitude: p.latitude, longitude: p.longitude, label: this.label() ?? '' } : null;
  }

  mapsLink(): string {
    const p = this.point()!;
    return `https://www.google.com/maps/search/?api=1&query=${p.latitude},${p.longitude}`;
  }
}
