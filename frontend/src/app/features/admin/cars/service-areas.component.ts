import { Component, ElementRef, OnDestroy, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GeoPoint, PickupCheck, ServiceArea } from '../../../core/models/location.model';
import { AdminCarService } from '../../../core/services/admin-car.service';
import { DEFAULT_MAP_CENTER, GoogleMapsService, PickedPlace } from '../../../core/services/google-maps.service';
import { ToastService } from '../../../core/services/toast.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog.component';
import { PlaceSearchComponent } from '../../../shared/components/place-search.component';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { apiErrorMessage } from '../../../shared/utils/api-error';

type LoadState = 'loading' | 'ready' | 'error';

interface Draft {
  id: number | null;
  name: string;
  isActive: boolean;
  pincodes: string;
}

const COLORS = { active: '#0F6F5C', off: '#6A7478', editing: '#9A6A11', ok: '#0F6F5C', no: '#C0483A' };

/**
 * /admin/service-areas — where cars pick customers up from. Each area is a zone drawn on the map and/or a
 * list of PIN codes; a pickup inside any active area can be booked, anything else shows "unavailable".
 */
@Component({
  selector: 'app-admin-service-areas',
  standalone: true,
  imports: [FormsModule, StatePanelComponent, PlaceSearchComponent, ConfirmDialogComponent],
  template: `
    <div class="ahead">
      <div>
        <h2>Pickup areas</h2>
        <div class="sub">Customers can book a car only when their pickup is inside an active zone or PIN code below.</div>
      </div>
      @if (state() === 'ready' && !draft()) { <button class="btn sm" (click)="startNew()">Add area</button> }
    </div>

    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <app-state-panel kind="error" message="Could not load pickup areas."></app-state-panel> }
      @case ('ready') {
        @if (activeCount() === 0) {
          <div class="panel acar-missing" style="margin-bottom:14px">
            <b>No active areas.</b> Right now customers can book a pickup from anywhere. Add an area to limit bookings to the places you serve.
          </div>
        }

        <div class="sa-grid">
          <div class="sa-side">
            @if (draft(); as d) {
              <div class="panel">
                <h4>{{ d.id === null ? 'New area' : 'Edit area' }}</h4>
                <div class="fld" style="margin-top:12px">
                  <label class="lbl" for="sa-name">Name</label>
                  <input id="sa-name" class="inp" maxlength="100" placeholder="e.g. Bhubaneswar city" [(ngModel)]="d.name" />
                </div>

                <div class="fld">
                  <span class="lbl">Zone on the map</span>
                  @if (drawing()) {
                    <p class="note">Click the map to add corners ({{ corners() }} so far). Drag the white handles to adjust, right-click a corner to remove it.</p>
                    <button class="btn sm soft" style="margin-top:8px" (click)="finishDrawing()" [disabled]="corners() > 0 && corners() < 3">Done drawing</button>
                  } @else {
                    <p class="note">{{ corners() >= 3 ? 'Zone with ' + corners() + ' corners. Drag the handles on the map to adjust.' : 'No zone drawn.' }}</p>
                    <div class="row" style="gap:8px;margin-top:8px">
                      <button class="btn sm ghost" (click)="startDrawing()">{{ corners() >= 3 ? 'Add corners' : 'Draw zone' }}</button>
                      @if (corners() > 0) { <button class="btn sm dang" (click)="clearZone()">Clear zone</button> }
                    </div>
                  }
                </div>

                <div class="fld">
                  <label class="lbl" for="sa-pins">PIN codes <span class="mut">(optional)</span></label>
                  <textarea id="sa-pins" class="inp" rows="3" placeholder="751001, 751002, 751003" [(ngModel)]="d.pincodes"></textarea>
                  <div class="note">Separate with commas, spaces or new lines. A pickup with one of these PIN codes is accepted even outside the zone.</div>
                </div>

                <label class="row sa-check">
                  <input type="checkbox" [(ngModel)]="d.isActive" /> <span>Active — customers can book pickups here</span>
                </label>

                @if (formError()) { <div class="errorbox" style="margin-top:10px">{{ formError() }}</div> }
                <div class="row" style="gap:8px;margin-top:14px;flex-wrap:wrap">
                  <button class="btn sm" [disabled]="saving() || drawing()" (click)="save()">
                    @if (saving()) { <span class="spin"></span> } @else { Save area }
                  </button>
                  <button class="btn sm ghost" [disabled]="saving()" (click)="cancel()">Cancel</button>
                  @if (d.id !== null) { <button class="btn sm dang" style="margin-left:auto" [disabled]="saving()" (click)="confirmDelete.set(true)">Delete</button> }
                </div>
              </div>
            } @else {
              <div class="panel">
                <h4>Areas</h4>
                @if (areas().length === 0) {
                  <p class="note" style="margin-top:8px">No areas yet.</p>
                } @else {
                  <ul class="sa-list">
                    @for (a of areas(); track a.serviceAreaId) {
                      <li (click)="edit(a)" (mouseenter)="highlight(a.serviceAreaId)" (mouseleave)="highlight(null)">
                        <div>
                          <b>{{ a.name }}</b>
                          <span class="note">{{ describe(a) }}</span>
                        </div>
                        <span [class]="'badge ' + (a.isActive ? 'ok' : '')">{{ a.isActive ? 'Active' : 'Off' }}</span>
                      </li>
                    }
                  </ul>
                }
              </div>
            }

            <div class="panel" style="margin-top:14px">
              <h4>Test a location</h4>
              <div class="sub">Check a pickup against the saved areas, exactly as the booking page will.</div>
              <app-place-search inputId="sa-test" placeholder="Search a place to test" [allowCurrentLocation]="true" (picked)="test($event)"></app-place-search>
              @if (testResult(); as r) {
                <p class="sa-result" [class.ok]="r.isServiceable">
                  {{ r.isServiceable ? '✓ Cars pick up here' + (r.areaName ? ' — ' + r.areaName : ' (no areas active)') : '✕ ' + (r.message || 'Outside all areas') }}
                </p>
              }
            </div>
          </div>

          <div class="sa-map" #mapEl>
            @if (mapError()) { <div class="sa-maperr">{{ mapError() }}</div> }
          </div>
        </div>
      }
    }

    <app-confirm-dialog
      [open]="confirmDelete()"
      title="Delete this area?"
      message="Pickups inside it can no longer be booked unless another active area covers them."
      confirmLabel="Delete"
      [destructive]="true"
      (confirm)="remove()"
      (cancel)="confirmDelete.set(false)"
    ></app-confirm-dialog>
  `,
  styles: `
    .sa-grid { display: grid; grid-template-columns: 340px 1fr; gap: 14px; align-items: start; }
    .sa-map { position: sticky; top: 16px; height: calc(100vh - 170px); min-height: 460px; border: 1px solid var(--line); border-radius: var(--radius-card); overflow: hidden; background: #fff; }
    .sa-maperr { position: absolute; inset: 0; display: grid; place-items: center; padding: 20px; text-align: center; color: var(--muted); font-size: 13px; }
    .sa-list { list-style: none; margin: 10px 0 0; padding: 0; }
    .sa-list li { display: flex; align-items: center; justify-content: space-between; gap: 10px; padding: 10px; margin: 0 -10px; border-radius: 10px; cursor: pointer; }
    .sa-list li:hover { background: var(--canvas); }
    .sa-list li b { display: block; font-weight: 500; font-size: 13px; }
    .sa-check { gap: 8px; font-size: 12.5px; cursor: pointer; }
    .sa-check input { accent-color: var(--accent); width: 16px; height: 16px; }
    .sa-result { margin-top: 10px; font-size: 12.5px; font-weight: 600; color: var(--danger); }
    .sa-result.ok { color: var(--ok); }
    @media (max-width: 900px) {
      .sa-grid { grid-template-columns: 1fr; }
      .sa-map { position: relative; top: 0; height: 420px; min-height: 0; order: -1; }
    }
  `,
})
export class AdminServiceAreasComponent implements OnInit, OnDestroy {
  private readonly cars = inject(AdminCarService);
  private readonly maps = inject(GoogleMapsService);
  private readonly toast = inject(ToastService);
  private readonly mapEl = viewChild<ElementRef<HTMLDivElement>>('mapEl');

  readonly state = signal<LoadState>('loading');
  readonly areas = signal<ServiceArea[]>([]);
  readonly draft = signal<Draft | null>(null);
  readonly drawing = signal(false);
  readonly corners = signal(0);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly mapError = signal<string | null>(null);
  readonly testResult = signal<PickupCheck | null>(null);
  readonly confirmDelete = signal(false);
  readonly activeCount = computed(() => this.areas().filter((a) => a.isActive).length);

  private map?: google.maps.Map;
  private shapes = new Map<number, google.maps.Polygon>();
  private editPoly?: google.maps.Polygon;
  private testMarker?: google.maps.marker.AdvancedMarkerElement;
  private listeners: google.maps.MapsEventListener[] = [];

  ngOnInit(): void {
    this.cars.serviceAreas().subscribe({
      next: (list) => {
        this.areas.set(list);
        this.state.set('ready');
        // The map container exists once the 'ready' view has rendered.
        setTimeout(() => void this.initMap());
      },
      error: () => this.state.set('error'),
    });
  }

  private async initMap(): Promise<void> {
    const el = this.mapEl()?.nativeElement;
    if (!el) return;
    try {
      await this.maps.load();
      const { Map } = (await google.maps.importLibrary('maps')) as google.maps.MapsLibrary;
      this.map = new Map(el, {
        center: DEFAULT_MAP_CENTER,
        zoom: 11,
        mapId: this.maps.mapId,
        streetViewControl: false,
        mapTypeControl: false,
        clickableIcons: false,
      });
      this.listeners.push(this.map.addListener('click', (e: google.maps.MapMouseEvent) => this.addCorner(e.latLng)));
      this.drawAll(true);
    } catch (e) {
      this.mapError.set(e instanceof Error ? e.message : 'Could not load the map.');
    }
  }

  /** Saved areas as read-only shapes; the one being edited is shown by the editable polygon instead. */
  private drawAll(fit = false): void {
    if (!this.map) return;
    this.shapes.forEach((s) => s.setMap(null));
    this.shapes.clear();
    const editingId = this.draft()?.id ?? null;
    const bounds = new google.maps.LatLngBounds();
    for (const a of this.areas()) {
      if (a.boundary.length < 3) continue;
      a.boundary.forEach((p) => bounds.extend({ lat: p.latitude, lng: p.longitude }));
      if (a.serviceAreaId === editingId) continue;
      const color = a.isActive ? COLORS.active : COLORS.off;
      const shape = new google.maps.Polygon({
        map: this.map,
        paths: a.boundary.map((p) => ({ lat: p.latitude, lng: p.longitude })),
        strokeColor: color,
        strokeWeight: 2,
        strokeOpacity: a.isActive ? 0.9 : 0.5,
        fillColor: color,
        fillOpacity: a.isActive ? 0.12 : 0.05,
        clickable: this.draft() === null,
      });
      shape.addListener('click', () => this.edit(a));
      this.shapes.set(a.serviceAreaId, shape);
    }
    if (fit && !bounds.isEmpty()) this.map.fitBounds(bounds, 40);
  }

  highlight(id: number | null): void {
    this.shapes.forEach((s, key) => s.setOptions({ strokeWeight: key === id ? 4 : 2 }));
  }

  describe(a: ServiceArea): string {
    const parts: string[] = [];
    if (a.boundary.length >= 3) parts.push('Map zone');
    if (a.pincodes.length) parts.push(`${a.pincodes.length} PIN code${a.pincodes.length === 1 ? '' : 's'}`);
    return parts.join(' · ');
  }

  startNew(): void {
    this.open({ id: null, name: '', isActive: true, pincodes: '' }, []);
    this.startDrawing();
  }

  edit(a: ServiceArea): void {
    if (this.draft()) return;
    this.open({ id: a.serviceAreaId, name: a.name, isActive: a.isActive, pincodes: a.pincodes.join(', ') }, a.boundary);
    if (this.map && a.boundary.length >= 3) {
      const b = new google.maps.LatLngBounds();
      a.boundary.forEach((p) => b.extend({ lat: p.latitude, lng: p.longitude }));
      this.map.fitBounds(b, 60);
    }
  }

  private open(draft: Draft, boundary: GeoPoint[]): void {
    this.formError.set(null);
    this.draft.set(draft);
    this.drawing.set(false);
    this.drawAll();
    this.editPoly?.setMap(null);
    this.editPoly = undefined;
    if (!this.map) return;
    this.editPoly = new google.maps.Polygon({
      map: this.map,
      paths: boundary.map((p) => ({ lat: p.latitude, lng: p.longitude })),
      editable: true,
      strokeColor: COLORS.editing,
      strokeWeight: 2,
      fillColor: COLORS.editing,
      fillOpacity: 0.15,
    });
    const path = this.editPoly.getPath();
    const sync = () => this.corners.set(path.getLength());
    path.addListener('insert_at', sync);
    path.addListener('remove_at', sync);
    this.editPoly.addListener('click', (e: google.maps.PolyMouseEvent) => this.addCorner(e.latLng));
    this.editPoly.addListener('rightclick', (e: google.maps.PolyMouseEvent) => {
      if (e.vertex !== undefined) path.removeAt(e.vertex);
    });
    sync();
  }

  startDrawing(): void {
    this.drawing.set(true);
    this.map?.setOptions({ draggableCursor: 'crosshair' });
  }

  finishDrawing(): void {
    this.drawing.set(false);
    this.map?.setOptions({ draggableCursor: null });
  }

  clearZone(): void {
    this.editPoly?.getPath().clear();
    this.corners.set(0);
  }

  private addCorner(latLng: google.maps.LatLng | null): void {
    if (!this.drawing() || !latLng || !this.editPoly) return;
    this.editPoly.getPath().push(latLng);
  }

  cancel(): void {
    this.finishDrawing();
    this.close();
  }

  private close(): void {
    this.editPoly?.setMap(null);
    this.editPoly = undefined;
    this.corners.set(0);
    this.draft.set(null);
    this.drawAll();
  }

  save(): void {
    const d = this.draft();
    if (!d) return;
    const boundary: GeoPoint[] = (this.editPoly?.getPath().getArray() ?? []).map((ll) => ({ latitude: ll.lat(), longitude: ll.lng() }));
    const pincodes = d.pincodes.split(/[\s,;]+/).map((p) => p.trim()).filter(Boolean);
    const bad = pincodes.filter((p) => !/^[1-9]\d{5}$/.test(p));
    if (!d.name.trim()) return this.formError.set('Give the area a name.');
    if (boundary.length > 0 && boundary.length < 3) return this.formError.set('A zone needs at least 3 corners.');
    if (bad.length) return this.formError.set(`Not a 6-digit PIN code: ${bad.join(', ')}`);
    if (!boundary.length && !pincodes.length) return this.formError.set('Draw a zone on the map or add at least one PIN code.');

    this.saving.set(true);
    this.formError.set(null);
    this.cars.saveServiceArea(d.id, { name: d.name.trim(), isActive: d.isActive, boundary, pincodes }).subscribe({
      next: (saved) => {
        this.areas.update((list) => [...list.filter((a) => a.serviceAreaId !== saved.serviceAreaId), saved].sort(byName));
        this.saving.set(false);
        this.testResult.set(null);
        this.close();
        this.toast.success('Pickup area saved.');
      },
      error: (e) => {
        this.saving.set(false);
        this.formError.set(apiErrorMessage(e, 'Could not save this area.'));
      },
    });
  }

  remove(): void {
    const id = this.draft()?.id;
    this.confirmDelete.set(false);
    if (id == null) return;
    this.saving.set(true);
    this.cars.deleteServiceArea(id).subscribe({
      next: () => {
        this.areas.update((list) => list.filter((a) => a.serviceAreaId !== id));
        this.saving.set(false);
        this.testResult.set(null);
        this.close();
        this.toast.success('Pickup area deleted.');
      },
      error: (e) => {
        this.saving.set(false);
        this.formError.set(apiErrorMessage(e, 'Could not delete this area.'));
      },
    });
  }

  async test(place: PickedPlace): Promise<void> {
    this.testResult.set(null);
    this.cars.checkPickup({ latitude: place.latitude, longitude: place.longitude }).subscribe({
      next: (r) => this.testResult.set(r),
      error: (e) => this.testResult.set({ isServiceable: false, areaName: null, message: apiErrorMessage(e, 'Could not check this place.') }),
    });
    if (!this.map) return;
    const { AdvancedMarkerElement } = (await google.maps.importLibrary('marker')) as google.maps.MarkerLibrary;
    const pos = { lat: place.latitude, lng: place.longitude };
    this.testMarker ??= new AdvancedMarkerElement({ map: this.map, title: place.label });
    this.testMarker.position = pos;
    this.map.panTo(pos);
  }

  ngOnDestroy(): void {
    this.listeners.forEach((l) => l.remove());
  }
}

function byName(a: ServiceArea, b: ServiceArea): number {
  return Number(b.isActive) - Number(a.isActive) || a.name.localeCompare(b.name);
}
