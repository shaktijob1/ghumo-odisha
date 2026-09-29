import { DecimalPipe } from '@angular/common';
import { Component, computed, input } from '@angular/core';
import { CarPricingTier } from '../../../core/models/car.model';
import { CarPricing } from '../../../core/models/driver.model';

interface TierRow {
  label: string;
  fare: number;
}

/** One pricing version: price per km, night halt and the base fare for each distance range. */
@Component({
  selector: 'app-pricing-summary',
  standalone: true,
  imports: [DecimalPipe],
  template: `
    @let p = pricing();
    <div class="kv">
      <span class="k">Price per km</span><b>₹{{ p.pricePerKm | number: '1.0-2' }}</b>
      <span class="k">Night halt</span><b>₹{{ p.nightHaltPrice | number: '1.0-0' }} / night</b>
      @for (t of tiers(); track t.label) {
        <span class="k">{{ t.label }}</span><b>₹{{ t.fare | number: '1.0-0' }} base fare</b>
      }
    </div>
  `,
})
export class PricingSummaryComponent {
  readonly pricing = input.required<CarPricing>();

  readonly tiers = computed<TierRow[]>(() => tierRows(this.pricing().tiers));
}

/** [{100, 900}, {null, 500}] → "Up to 100 km ₹900", "Above 100 km ₹500". */
export function tierRows(tiers: CarPricingTier[]): TierRow[] {
  const sorted = [...tiers].sort((a, b) => (a.upToKm ?? Infinity) - (b.upToKm ?? Infinity));
  let prev = 0;
  return sorted.map((t) => {
    const label =
      t.upToKm === null ? (prev === 0 ? 'Any distance' : `Above ${prev} km`) : prev === 0 ? `Up to ${t.upToKm} km` : `${prev + 1}–${t.upToKm} km`;
    if (t.upToKm !== null) prev = t.upToKm;
    return { label, fare: t.baseFare };
  });
}
