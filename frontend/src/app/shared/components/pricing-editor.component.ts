import { CommonModule } from '@angular/common';
import { Component, effect, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CarPricing, SubmitPricingRequest } from '../../core/models/driver.model';

interface Row {
  upToKm: number | null;
  baseFare: number | null;
}

/**
 * Edits a car's pricing: price per km, night halt and base fare by distance ("up to 50 km ₹900,
 * up to 100 km ₹700 … above 200 km ₹300"). Used by drivers (proposal, needs approval) and admins
 * (applied directly). Only shapes the request — the server validates and prices everything.
 */
@Component({
  selector: 'app-pricing-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <form (ngSubmit)="save()">
      <div class="f2 dv-f2">
        <div class="fld">
          <label class="lbl" for="pe-km">Price per km (₹)</label>
          <input id="pe-km" class="inp" type="number" inputmode="decimal" min="1" step="0.5" name="perKm" [(ngModel)]="perKm" />
        </div>
        <div class="fld">
          <label class="lbl" for="pe-nh">Night halt per night (₹)</label>
          <input id="pe-nh" class="inp" type="number" inputmode="numeric" min="0" step="50" name="nightHalt" [(ngModel)]="nightHalt" />
        </div>
      </div>

      <span class="lbl">Base fare by distance</span>
      <p class="note" style="margin-bottom:8px">Short trips usually need a higher base fare. Add ranges; the last one covers everything above.</p>
      <div class="pe-rows">
        @for (r of rows(); track $index; let i = $index; let last = $last) {
          <div class="pe-row">
            @if (!last) {
              <span class="pe-lbl">{{ i === 0 ? 'Up to' : (prevLimit(i) + 1) + ' –' }}</span>
              <input class="inp" type="number" inputmode="numeric" min="1" [name]="'up' + i" [(ngModel)]="r.upToKm" aria-label="Up to km" />
              <span class="pe-lbl">km</span>
            } @else {
              <span class="pe-lbl pe-above">{{ rows().length === 1 ? 'Any distance' : 'Above ' + (prevLimit(i) || '…') + ' km' }}</span>
            }
            <span class="pe-lbl">₹</span>
            <input class="inp" type="number" inputmode="numeric" min="0" [name]="'fare' + i" [(ngModel)]="r.baseFare" aria-label="Base fare" />
            @if (!last) {
              <button type="button" class="pe-x" (click)="remove(i)" aria-label="Remove range">×</button>
            } @else {
              <span class="pe-x" aria-hidden="true"></span>
            }
          </div>
        }
      </div>
      <button type="button" class="linkbtn" style="padding:6px 0 12px" (click)="addRange()">+ Add distance range</button>

      @if (localError() || error()) { <div class="errorbox">{{ localError() || error() }}</div> }
      <button type="submit" class="btn" [disabled]="busy()">@if (busy()) { <span class="spin"></span> } @else { {{ submitLabel() }} }</button>
    </form>
  `,
})
export class PricingEditorComponent {
  readonly initial = input<CarPricing | null>(null);
  readonly defaultNightHalt = input(400);
  readonly submitLabel = input('Send pricing for approval');
  readonly busy = input(false);
  readonly error = input<string | null>(null);
  readonly submitted = output<SubmitPricingRequest>();

  perKm: number | null = null;
  nightHalt: number | null = null;
  readonly rows = signal<Row[]>([{ upToKm: 100, baseFare: null }, { upToKm: null, baseFare: null }]);
  readonly localError = signal<string | null>(null);


  constructor() {
    effect(() => {
      const p = this.initial();
      if (p) {
        this.perKm = p.pricePerKm;
        this.nightHalt = p.nightHaltPrice;
        const sorted = [...p.tiers].sort((a, b) => (a.upToKm ?? Infinity) - (b.upToKm ?? Infinity));
        this.rows.set(sorted.map((t) => ({ upToKm: t.upToKm, baseFare: t.baseFare })));
      } else {
        this.nightHalt ??= this.defaultNightHalt();
      }
    });
  }

  prevLimit(i: number): number {
    return i === 0 ? 0 : Number(this.rows()[i - 1].upToKm) || 0;
  }

  addRange(): void {
    const rows = this.rows();
    const lastLimit = rows.length > 1 ? Number(rows[rows.length - 2].upToKm) || 0 : 0;
    const next: Row = { upToKm: lastLimit + 50, baseFare: null };
    this.rows.set([...rows.slice(0, -1), next, rows[rows.length - 1]]);
  }

  remove(i: number): void {
    this.rows.set(this.rows().filter((_, idx) => idx !== i));
  }

  save(): void {
    this.localError.set(null);
    const rows = this.rows();
    if (!this.perKm || this.perKm <= 0) return this.localError.set('Enter your price per km.');
    if (this.nightHalt === null || this.nightHalt < 0) return this.localError.set('Enter the night halt amount (0 if none).');
    let prev = 0;
    for (let i = 0; i < rows.length; i++) {
      const r = rows[i];
      if (r.baseFare === null || r.baseFare < 0) return this.localError.set('Enter a base fare for every distance range.');
      if (i < rows.length - 1) {
        const up = Number(r.upToKm);
        if (!up || up <= prev) return this.localError.set('Each distance range must end after the previous one.');
        prev = up;
      }
    }
    this.submitted.emit({
      pricePerKm: Number(this.perKm),
      nightHaltPrice: Number(this.nightHalt),
      tiers: rows.map((r, i) => ({ upToKm: i === rows.length - 1 ? null : Number(r.upToKm), baseFare: Number(r.baseFare) })),
    });
  }
}
