import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BlogService } from '../../core/services/blog.service';
import { BlogPostSummary } from '../../core/models/blog.model';
import { StoryCardComponent } from '../../shared/components/story-card.component';

type LoadState = 'loading' | 'ready' | 'error';

/** Home page "News & Blog": the newest travel stories (same count the server-rendered page lists). */
@Component({
  selector: 'app-home-stories',
  standalone: true,
  imports: [RouterLink, StoryCardComponent],
  template: `
    @if (state() !== 'ready' || stories().length > 0) {
      <section id="travel-stories" class="sect">
        <div class="container">
          <div class="trend-panel hs-panel">
            <div class="sechead">
              <div>
                <span class="eyebrow">News &amp; Blog</span>
                <h2><span class="up-g">Inspiring Travel </span><span class="up-o">Stories</span></h2>
                <p class="sub">Guides and stories from the places we travel to — where to go, when to go and what not to miss.</p>
              </div>
              <a class="btn ghost sm" routerLink="/blog">View all stories →</a>
            </div>

            @switch (state()) {
              @case ('loading') {
                <div class="hs-grid">
                  @for (i of [1, 2, 3]; track i) {
                    <div class="tcard skel"><div class="shot"></div><div class="body"><div class="ln" style="width:70%"></div><div class="ln" style="width:55%"></div></div></div>
                  }
                </div>
              }
              @case ('error') {
                <div class="state-panel">
                  <p class="mut" style="margin-bottom:14px">Unable to load travel stories.</p>
                  <button class="btn sm" (click)="load()">Try Again</button>
                </div>
              }
              @case ('ready') {
                <div class="hs-grid">
                  @for (s of stories(); track s.blogPostId) {
                    <app-story-card [story]="s"></app-story-card>
                  }
                </div>
              }
            }
          </div>
        </div>
      </section>
    }
  `,
  styles: `
    .hs-panel { background: #fff; border: 1px solid var(--line); border-radius: var(--radius-card); padding: 26px 30px; }
    .hs-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 18px; }
    @media (max-width: 900px) { .hs-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 640px) {
      .hs-panel { padding: 16px 14px; }
      /* Phones: one swipeable row of cards. */
      .hs-grid { display: flex; overflow-x: auto; scroll-snap-type: x mandatory; gap: 12px; padding-bottom: 6px; }
      .hs-grid > * { flex: 0 0 82%; scroll-snap-align: start; }
    }
  `,
})
export class HomeStoriesComponent implements OnInit {
  /** Same as HomeStoryCount in the API's SeoPageRenderer. */
  private static readonly Count = 6;
  private readonly blogService = inject(BlogService);

  readonly state = signal<LoadState>('loading');
  readonly stories = signal<BlogPostSummary[]>([]);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.blogService.getStories(HomeStoriesComponent.Count).subscribe({
      next: (s) => {
        this.stories.set(s);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
