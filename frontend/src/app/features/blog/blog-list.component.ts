import { Component, OnInit, inject, signal } from '@angular/core';
import { BlogService } from '../../core/services/blog.service';
import { BlogPostSummary } from '../../core/models/blog.model';
import { BreadcrumbsComponent } from '../../shared/components/breadcrumbs.component';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { StoryCardComponent } from '../../shared/components/story-card.component';

type LoadState = 'loading' | 'ready' | 'error';

/** /blog — every published travel story, newest first. */
@Component({
  selector: 'app-blog-list',
  standalone: true,
  imports: [BreadcrumbsComponent, StatePanelComponent, StoryCardComponent],
  template: `
    <div class="container">
      <app-breadcrumbs [items]="[{ label: 'Travel stories' }]"></app-breadcrumbs>
      <header class="bl-head">
        <span class="eyebrow">News &amp; Blog</span>
        <h1><span class="up-g">Travel Stories &amp; </span><span class="up-o">Odisha Travel Guides</span></h1>
        <p>Inspiring travel stories from our group trips, and honest guides to the best places to visit in Odisha.</p>
      </header>

      @switch (state()) {
        @case ('loading') { <app-state-panel kind="loading" message="Loading stories…"></app-state-panel> }
        @case ('error') {
          <app-state-panel kind="error" message="Unable to load travel stories."></app-state-panel>
          <div style="text-align:center;margin-top:-8px"><button class="btn sm" (click)="load()">Try Again</button></div>
        }
        @case ('ready') {
          @if (stories().length === 0) {
            <app-state-panel kind="empty" message="No stories yet — check back soon."></app-state-panel>
          } @else {
            <div class="bl-grid">
              @for (s of stories(); track s.blogPostId) {
                <app-story-card [story]="s"></app-story-card>
              }
            </div>
          }
        }
      }
    </div>
  `,
  styles: `
    .bl-head { margin: 18px 0 26px; }
    .bl-head .eyebrow { display: block; margin-bottom: 8px; letter-spacing: .08em; text-transform: uppercase; }
    .bl-head h1 { font-size: 34px; font-weight: 800; margin: 0 0 8px; line-height: 1.2; }
    .bl-head p { color: var(--muted); font-size: 14.5px; max-width: 60ch; margin: 0; }
    .bl-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 20px; padding-bottom: 40px; }
    @media (max-width: 900px) { .bl-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    @media (max-width: 640px) {
      .bl-head h1 { font-size: 26px; }
      .bl-grid { grid-template-columns: 1fr; gap: 14px; }
    }
  `,
})
export class BlogListComponent implements OnInit {
  private readonly blogService = inject(BlogService);

  readonly state = signal<LoadState>('loading');
  readonly stories = signal<BlogPostSummary[]>([]);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.blogService.getStories(100).subscribe({
      next: (s) => {
        this.stories.set(s);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
