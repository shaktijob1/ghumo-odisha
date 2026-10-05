import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, map, of } from 'rxjs';
import { BlogService } from '../../core/services/blog.service';
import { TravelMoment } from '../../core/models/blog.model';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { PhotoLightboxComponent } from '../../shared/components/photo-lightbox.component';

/**
 * Home page "Real Travel Moments": the admin-uploaded trip photos (Admin dashboard → Real Travel
 * Moments) in a photo grid; tapping one opens it full screen. The section stays hidden until at
 * least one photo exists (or if loading fails) — it's a showcase, not something to wait for.
 */
@Component({
  selector: 'app-travel-moments',
  standalone: true,
  imports: [ImageUrlPipe, PhotoLightboxComponent],
  template: `
    @if (moments(); as list) {
      @if (list.length > 0) {
        <section id="travel-moments" class="sect">
          <div class="container">
            <div class="trend-panel tm-panel">
              <div class="sechead">
                <div>
                  <span class="eyebrow">From our trips</span>
                  <h2><span class="up-g">Real Travel </span><span class="up-o">Moments</span></h2>
                  <p class="sub">Real photos from Ghumo Odisha group trips — no stock images.</p>
                </div>
              </div>
              <ul class="tm-grid" [attr.data-count]="list.length">
                @for (m of list; track m.travelMomentId; let i = $index) {
                  <li>
                    <button type="button" class="tm-item" (click)="openIndex.set(i)" [attr.aria-label]="'View photo: ' + (m.caption ?? 'travel moment')">
                      <img [src]="m.imageUrl | imageUrl" [alt]="m.caption ?? 'A Ghumo Odisha group trip moment'" loading="lazy" decoding="async" />
                      @if (m.caption) { <span class="tm-cap">{{ m.caption }}</span> }
                    </button>
                  </li>
                }
              </ul>
            </div>
          </div>
        </section>
        <app-photo-lightbox [photos]="list" [(index)]="openIndex" altText="A Ghumo Odisha group trip moment"></app-photo-lightbox>
      }
    }
  `,
  styles: `
    .tm-panel { background: #fff; border: 1px solid var(--line); border-radius: var(--radius-card); padding: 26px 30px; }
    .tm-grid { list-style: none; margin: 0; padding: 0; display: grid; grid-template-columns: repeat(4, 1fr); grid-auto-rows: 190px; grid-auto-flow: dense; gap: 12px; }
    .tm-grid li { min-width: 0; }
    /* First photo is the big one; with 5+ photos the grid reads as a mosaic. */
    .tm-grid li:first-child { grid-column: span 2; grid-row: span 2; }
    .tm-grid[data-count="1"] { grid-template-columns: 1fr; grid-auto-rows: 360px; }
    .tm-grid[data-count="1"] li:first-child, .tm-grid[data-count="2"] li:first-child { grid-column: auto; grid-row: auto; }
    .tm-grid[data-count="2"] { grid-template-columns: repeat(2, 1fr); grid-auto-rows: 300px; }
    .tm-item { position: relative; display: block; width: 100%; height: 100%; padding: 0; border: 0; border-radius: 12px; overflow: hidden; cursor: zoom-in; background: var(--canvas); }
    .tm-item img { width: 100%; height: 100%; object-fit: cover; display: block; transition: transform .35s ease; }
    @media (hover: hover) { .tm-item:hover img { transform: scale(1.05); } }
    .tm-cap { position: absolute; left: 0; right: 0; bottom: 0; padding: 28px 12px 10px; text-align: left; color: #fff; font-size: 13px; font-weight: 600; background: linear-gradient(180deg, rgba(15,20,22,0), rgba(15,20,22,.78)); }
    @media (max-width: 900px) {
      .tm-grid { grid-template-columns: repeat(3, 1fr); grid-auto-rows: 150px; }
    }
    @media (max-width: 640px) {
      .tm-panel { padding: 16px 14px; }
      /* Phones: one swipeable row of tall photos. */
      .tm-grid, .tm-grid[data-count] { display: flex; overflow-x: auto; scroll-snap-type: x mandatory; gap: 10px; padding-bottom: 6px; }
      .tm-grid li, .tm-grid li:first-child { flex: 0 0 72%; height: 300px; scroll-snap-align: start; }
    }
  `,
})
export class TravelMomentsComponent {
  /** null while loading or after an error: the section simply doesn't show. */
  readonly moments = toSignal(
    inject(BlogService).getTravelMoments().pipe(
      map((list): TravelMoment[] | null => list),
      catchError(() => of(null)),
    ),
    { initialValue: null },
  );

  readonly openIndex = signal<number | null>(null);
}
