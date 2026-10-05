import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { BlogService } from '../../core/services/blog.service';
import { ContactService } from '../../core/services/contact.service';
import { BlogPostDetail } from '../../core/models/blog.model';
import { BreadcrumbsComponent } from '../../shared/components/breadcrumbs.component';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { StoryCardComponent } from '../../shared/components/story-card.component';
import { PhotoLightboxComponent } from '../../shared/components/photo-lightbox.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';

type LoadState = 'loading' | 'ready' | 'error' | 'not-found';

/**
 * /blog/{slug} — one travel story: hero photo, the article, its photos, "Popular tags" (the search
 * phrases the story targets) and more stories. The API renders the same content for search engines.
 */
@Component({
  selector: 'app-blog-post',
  standalone: true,
  imports: [DatePipe, RouterLink, BreadcrumbsComponent, StatePanelComponent, StoryCardComponent, PhotoLightboxComponent, ImageUrlPipe],
  template: `
    @switch (state()) {
      @case ('loading') {
        <div class="container"><app-state-panel kind="loading"></app-state-panel></div>
      }
      @case ('error') {
        <div class="container">
          <app-state-panel kind="error" message="Unable to load this story."></app-state-panel>
          <div style="text-align:center;margin-top:-8px"><button class="btn sm" (click)="load()">Try Again</button></div>
        </div>
      }
      @case ('not-found') {
        <div class="container">
          <app-state-panel kind="empty" message="We couldn't find that story."></app-state-panel>
          <div style="text-align:center;margin-top:-8px"><a class="btn sm" routerLink="/blog">See all travel stories</a></div>
        </div>
      }
      @case ('ready') {
        @let p = post()!;
        <div class="container">
          <app-breadcrumbs [items]="[{ label: 'Travel stories', link: '/blog' }, { label: p.title }]"></app-breadcrumbs>

          <section class="lhero bp-hero">
            @if (p.heroImageUrl) {
              <img [src]="p.heroImageUrl | imageUrl" [alt]="p.title" fetchpriority="high" />
            }
            <div class="lhero-inner bp-hero-inner">
              <div>
                @if (p.place) { <span class="bp-place">{{ p.place }}</span> }
                <h1>{{ p.title }}</h1>
                <p>@if (p.publishedAt) { {{ p.publishedAt | date: 'd MMMM y' }} · }{{ p.readMinutes }} min read</p>
              </div>
            </div>
          </section>

          @if (p.photos.length > 0) {
            <section class="bp-photos" [attr.aria-label]="(p.place ?? 'Trip') + ' photos'">
              @for (ph of p.photos; track ph.blogPostPhotoId; let i = $index) {
                <button type="button" class="bp-photo" (click)="openPhoto.set(i)" [attr.aria-label]="'View photo: ' + (ph.caption ?? p.title)">
                  <img [src]="ph.imageUrl | imageUrl" [alt]="ph.caption ?? p.title" loading="lazy" decoding="async" />
                </button>
              }
            </section>
            <app-photo-lightbox [photos]="p.photos" [(index)]="openPhoto" [altText]="p.title"></app-photo-lightbox>
          }

          <div class="bp-wrap">
            <article class="bp-article">
              <p class="bp-lead">{{ p.excerpt }}</p>
              @for (b of p.blocks; track $index) {
                @switch (b.type) {
                  @case ('h2') { <h2>{{ b.text }}</h2> }
                  @case ('ul') {
                    <ul>
                      @for (item of b.items; track $index) { <li>{{ item }}</li> }
                    </ul>
                  }
                  @default { <p>{{ b.text }}</p> }
                }
              }
            </article>

            <aside class="bp-cta">
              <div>
                <b>Plan your {{ p.place ?? 'Odisha' }} trip with Ghumo Odisha</b>
                <span>Group trips from Bhubaneswar with transport, stays and a day-wise itinerary.</span>
              </div>
              <div class="bp-cta-btns">
                <a class="btn" routerLink="/" fragment="upcoming-trips">See upcoming trips</a>
                <button type="button" class="btn ghost" (click)="askOnWhatsApp(p)">Ask on WhatsApp</button>
              </div>
            </aside>

            @if (p.tags.length > 0) {
              <section class="bp-tags" aria-labelledby="bp-tags-title">
                <h2 id="bp-tags-title">Popular <span class="up-o">tags</span></h2>
                <ul>
                  @for (t of p.tags; track t) { <li>#{{ t }}</li> }
                </ul>
              </section>
            }
          </div>

          @if (p.moreStories.length > 0) {
            <section class="sect bp-more">
              <div class="sechead">
                <div><h2>More <span class="up-g">travel </span><span class="up-o">stories</span></h2></div>
                <a class="btn ghost sm" routerLink="/blog">View all stories →</a>
              </div>
              <div class="bp-more-grid">
                @for (s of p.moreStories; track s.blogPostId) { <app-story-card [story]="s"></app-story-card> }
              </div>
            </section>
          }
        </div>
      }
    }
  `,
  styles: `
    .bp-hero { min-height: clamp(260px, 52vh, 440px); min-height: clamp(260px, 52svh, 440px); }
    .bp-hero-inner { padding-bottom: 30px; }
    .bp-hero-inner h1 { font-size: 38px; line-height: 1.2; max-width: 22ch; }
    .bp-hero-inner p { font-size: 14px; }
    .bp-place { display: inline-block; background: rgba(255,255,255,.94); color: var(--accent); font-size: 12px; font-weight: 700; padding: 5px 12px; border-radius: 999px; }
    .bp-photos { display: flex; gap: 10px; overflow-x: auto; margin-top: 12px; padding-bottom: 4px; scroll-snap-type: x mandatory; }
    .bp-photo { flex: 0 0 auto; width: 210px; height: 140px; padding: 0; border: 0; border-radius: 12px; overflow: hidden; cursor: zoom-in; background: var(--canvas); scroll-snap-align: start; }
    .bp-photo img { width: 100%; height: 100%; object-fit: cover; display: block; transition: transform .3s ease; }
    @media (hover: hover) { .bp-photo:hover img { transform: scale(1.06); } }
    .bp-wrap { max-width: 800px; margin: 0 auto; padding-top: 28px; }
    .bp-article { color: var(--ink); font-size: 16px; line-height: 1.75; }
    .bp-article .bp-lead { font-size: 18px; line-height: 1.6; color: var(--ink); font-weight: 500; margin: 0 0 18px; }
    .bp-article h2 { font-size: 23px; line-height: 1.3; margin: 32px 0 10px; }
    .bp-article p { margin: 0 0 14px; }
    .bp-article ul { margin: 0 0 16px; padding-left: 22px; }
    .bp-article li { margin: 6px 0; }
    .bp-cta { display: flex; align-items: center; justify-content: space-between; gap: 16px; flex-wrap: wrap; margin: 30px 0; padding: 18px 20px; border: 1px solid #BFDDD4; border-radius: var(--radius-card); background: var(--accent-soft); }
    .bp-cta b { display: block; font-size: 16px; margin-bottom: 3px; }
    .bp-cta span { font-size: 13.5px; color: var(--muted); }
    .bp-cta-btns { display: flex; gap: 10px; flex-wrap: wrap; }
    .bp-tags { border: 1px solid var(--line); border-radius: var(--radius-card); padding: 20px 22px; background: #fff; }
    .bp-tags h2 { font-size: 20px; margin: 0 0 14px; }
    .bp-tags ul { display: flex; flex-wrap: wrap; gap: 8px; list-style: none; margin: 0; padding: 0; }
    .bp-tags li { font-size: 13px; color: var(--ink); background: var(--canvas); border: 1px solid var(--line); border-radius: 999px; padding: 6px 12px; }
    .bp-more { padding-top: 40px; }
    .bp-more .sechead h2 { font-size: 28px; }
    .bp-more-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 18px; }
    @media (max-width: 900px) { .bp-more-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 640px) {
      .bp-hero-inner { padding: 20px 18px 22px; }
      .bp-hero-inner h1 { font-size: 26px; }
      .bp-photo { width: 160px; height: 112px; }
      .bp-wrap { padding-top: 20px; }
      .bp-article { font-size: 15.5px; }
      .bp-article .bp-lead { font-size: 16.5px; }
      .bp-article h2 { font-size: 20px; margin-top: 26px; }
      .bp-cta-btns, .bp-cta-btns .btn { width: 100%; }
      .bp-tags { padding: 16px 14px; }
      .bp-more .sechead h2 { font-size: 22px; }
      .bp-more-grid { grid-template-columns: 1fr; }
    }
  `,
})
export class BlogPostComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly blogService = inject(BlogService);
  private readonly contactService = inject(ContactService);

  readonly state = signal<LoadState>('loading');
  readonly post = signal<BlogPostDetail | null>(null);
  readonly openPhoto = signal<number | null>(null);
  private slug = '';

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get('slug');
      if (!slug) return;
      this.slug = slug;
      this.openPhoto.set(null);
      this.load();
    });
  }

  load(): void {
    this.state.set('loading');
    this.blogService.getStory(this.slug).subscribe({
      next: (p) => {
        this.post.set(p);
        this.state.set('ready');
      },
      error: (err) => this.state.set(err?.status === 404 ? 'not-found' : 'error'),
    });
  }

  askOnWhatsApp(p: BlogPostDetail): void {
    const message = `Hi! I read "${p.title}" and would like to plan a ${p.place ?? 'Odisha'} trip.`;
    window.open(this.contactService.buildWhatsAppLink(message), '_blank');
  }
}
