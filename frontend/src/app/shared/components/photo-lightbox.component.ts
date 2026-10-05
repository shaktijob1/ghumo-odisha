import { Component, HostListener, computed, input, model } from '@angular/core';
import { ImageUrlPipe } from '../pipes/image-url.pipe';

export interface LightboxPhoto {
  imageUrl: string;
  caption: string | null;
}

/** Full-screen photo viewer: Esc / tap outside closes, arrows (or ← →) step through the photos. */
@Component({
  selector: 'app-photo-lightbox',
  standalone: true,
  imports: [ImageUrlPipe],
  template: `
    @if (current(); as p) {
      <div class="lb" role="dialog" aria-modal="true" [attr.aria-label]="p.caption ?? altText()" (click)="close()">
        <button type="button" class="lb-x" aria-label="Close" (click)="close()">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><path d="M18 6 6 18M6 6l12 12"></path></svg>
        </button>
        @if (photos().length > 1) {
          <button type="button" class="lb-nav prev" aria-label="Previous photo" (click)="step(-1); $event.stopPropagation()">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="m15 18-6-6 6-6"></path></svg>
          </button>
          <button type="button" class="lb-nav next" aria-label="Next photo" (click)="step(1); $event.stopPropagation()">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="m9 6 6 6-6 6"></path></svg>
          </button>
        }
        <figure (click)="$event.stopPropagation()">
          <img [src]="p.imageUrl | imageUrl" [alt]="p.caption ?? altText()" />
          <figcaption>{{ p.caption }} <span>{{ (index() ?? 0) + 1 }} / {{ photos().length }}</span></figcaption>
        </figure>
      </div>
    }
  `,
  styles: `
    .lb { position: fixed; inset: 0; z-index: 1000; background: rgba(10,14,15,.92); display: grid; place-items: center; padding: 24px 64px; }
    .lb figure { margin: 0; max-width: min(1100px, 100%); }
    .lb img { display: block; max-width: 100%; max-height: 80vh; max-height: 80svh; border-radius: 12px; margin: 0 auto; }
    .lb figcaption { display: flex; justify-content: space-between; gap: 16px; color: #fff; font-size: 14px; margin-top: 10px; }
    .lb figcaption span { color: rgba(255,255,255,.65); flex-shrink: 0; margin-left: auto; }
    .lb-x, .lb-nav { position: absolute; display: grid; place-items: center; width: 44px; height: 44px; border-radius: 50%; border: 0; background: rgba(255,255,255,.14); color: #fff; cursor: pointer; }
    .lb-x { top: 16px; right: 16px; }
    .lb-nav { top: 50%; transform: translateY(-50%); }
    .lb-nav.prev { left: 12px; }
    .lb-nav.next { right: 12px; }
    @media (max-width: 640px) {
      .lb { padding: 16px 8px; }
      .lb-nav { top: auto; bottom: 18px; transform: none; }
    }
  `,
})
export class PhotoLightboxComponent {
  readonly photos = input.required<LightboxPhoto[]>();
  /** The open photo's position; null = closed. */
  readonly index = model<number | null>(null);
  readonly altText = input('Ghumo Odisha trip photo');

  readonly current = computed(() => {
    const i = this.index();
    return i === null ? null : (this.photos()[i] ?? null);
  });

  @HostListener('document:keydown.escape')
  close(): void {
    this.index.set(null);
  }

  @HostListener('document:keydown.arrowleft')
  onLeft(): void {
    if (this.index() !== null) this.step(-1);
  }

  @HostListener('document:keydown.arrowright')
  onRight(): void {
    if (this.index() !== null) this.step(1);
  }

  step(direction: 1 | -1): void {
    const count = this.photos().length;
    const i = this.index();
    if (i === null || count === 0) return;
    this.index.set((i + direction + count) % count);
  }
}
