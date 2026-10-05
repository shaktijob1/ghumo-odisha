import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BlogPostSummary } from '../../core/models/blog.model';
import { ImageUrlPipe } from '../pipes/image-url.pipe';

/** A travel story card: photo, place, read time, title, excerpt. Opens /blog/{slug}. */
@Component({
  selector: 'app-story-card',
  standalone: true,
  imports: [RouterLink, DatePipe, ImageUrlPipe],
  template: `
    @let s = story();
    <a class="scard" [routerLink]="['/blog', s.slug]">
      <div class="scard-ph">
        @if (s.heroImageUrl) {
          <img [src]="s.heroImageUrl | imageUrl" [alt]="s.title" loading="lazy" decoding="async" />
        } @else {
          <span class="scard-ph-txt" aria-hidden="true">{{ s.place ?? 'Odisha' }}</span>
        }
        @if (s.place) { <span class="scard-place">{{ s.place }}</span> }
      </div>
      <div class="scard-body">
        <span class="scard-meta">
          @if (s.publishedAt) { {{ s.publishedAt | date: 'd MMM y' }} · }{{ s.readMinutes }} min read
        </span>
        <h3>{{ s.title }}</h3>
        <p>{{ s.excerpt }}</p>
        <span class="scard-more">Read story →</span>
      </div>
    </a>
  `,
  styles: `
    :host { display: block; min-width: 0; }
    .scard { display: flex; flex-direction: column; height: 100%; background: #fff; border: 1px solid var(--line); border-radius: var(--radius-card); overflow: hidden; color: var(--ink); text-decoration: none; transition: transform .2s ease, box-shadow .2s ease, border-color .2s ease; }
    @media (hover: hover) { .scard:hover { transform: translateY(-3px); border-color: var(--accent); box-shadow: 0 0 0 1px var(--accent), 0 12px 26px rgba(15,111,92,.16); } }
    .scard-ph { position: relative; aspect-ratio: 16 / 10; background: linear-gradient(150deg, var(--accent), #24312E); overflow: hidden; }
    .scard-ph img { width: 100%; height: 100%; object-fit: cover; display: block; transition: transform .3s ease; }
    @media (hover: hover) { .scard:hover .scard-ph img { transform: scale(1.05); } }
    .scard-ph-txt { position: absolute; inset: 0; display: grid; place-items: center; color: rgba(255,255,255,.9); font-size: 26px; font-weight: 800; letter-spacing: .04em; text-transform: uppercase; }
    .scard-place { position: absolute; left: 12px; top: 12px; background: rgba(255,255,255,.94); color: var(--accent); font-size: 11.5px; font-weight: 700; padding: 5px 10px; border-radius: 999px; }
    .scard-body { display: flex; flex-direction: column; gap: 6px; padding: 14px 16px 16px; flex: 1; }
    .scard-meta { font-size: 12px; color: var(--muted); }
    .scard h3 { font-size: 16.5px; line-height: 1.35; margin: 0; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
    .scard p { font-size: 13.5px; line-height: 1.55; color: var(--muted); margin: 0; display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden; }
    .scard-more { margin-top: auto; padding-top: 6px; font-size: 13px; font-weight: 600; color: var(--accent); }
  `,
})
export class StoryCardComponent {
  readonly story = input.required<BlogPostSummary>();
}
