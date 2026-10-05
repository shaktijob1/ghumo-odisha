import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminBlogService } from '../../../core/services/admin-blog.service';
import { AdminBlogPostListItem } from '../../../core/models/blog.model';
import { ToastService } from '../../../core/services/toast.service';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog.component';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';

type LoadState = 'loading' | 'ready' | 'error';

/** Admin → Stories: the travel stories shown in the home page's "News & Blog" and at /blog. */
@Component({
  selector: 'app-admin-blog-list',
  standalone: true,
  imports: [DatePipe, RouterLink, StatePanelComponent, ConfirmDialogComponent, ImageUrlPipe],
  template: `
    <div class="ahead">
      <div>
        <h2>Stories</h2>
        <div class="sub">Travel stories for the home page's "News &amp; Blog" section and the /blog pages</div>
      </div>
      <a class="btn sm" routerLink="/admin/stories/add">Write a story</a>
    </div>

    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <app-state-panel kind="error" message="Could not load stories."></app-state-panel> }
      @case ('ready') {
        @if (posts().length === 0) {
          <app-state-panel kind="empty" message="No stories yet. Write your first one."></app-state-panel>
        } @else {
          <div class="panel" style="padding:16px">
            <div class="tblwrap">
              <table class="tbl">
                <tr><th></th><th>Story</th><th>Place</th><th>Photos</th><th>Tags</th><th>Status</th><th>Updated</th><th></th></tr>
                @for (p of posts(); track p.blogPostId) {
                  <tr>
                    <td style="width:64px">
                      @if (p.heroImageUrl) {
                        <img [src]="p.heroImageUrl | imageUrl" alt="" style="width:56px;height:36px;border-radius:8px;object-fit:cover;display:block" />
                      } @else {
                        <div style="width:56px;height:36px;border-radius:8px;background:var(--canvas);border:1px dashed var(--line)" title="No hero photo yet"></div>
                      }
                    </td>
                    <td><b>{{ p.title }}</b><div class="note">/blog/{{ p.slug }}</div></td>
                    <td>{{ p.place ?? '—' }}</td>
                    <td>{{ p.photoCount }}</td>
                    <td>{{ p.tagCount }}</td>
                    <td><span class="badge" [class.ok]="p.isPublished">{{ p.isPublished ? 'Published' : 'Draft' }}</span></td>
                    <td class="mut">{{ p.updatedAt | date: 'd MMM y' }}</td>
                    <td class="num">
                      <div class="row" style="gap:5px;justify-content:flex-end">
                        @if (p.isPublished) {
                          <a class="sel" style="padding:5px 9px" [href]="'/blog/' + p.slug" target="_blank" rel="noopener">View</a>
                        }
                        <a class="sel" style="padding:5px 9px" [routerLink]="['/admin/stories', p.blogPostId, 'edit']">Edit</a>
                        <span class="sel" style="padding:5px 9px;color:var(--danger);cursor:pointer" (click)="deleteTarget.set(p)">Delete</span>
                      </div>
                    </td>
                  </tr>
                }
              </table>
            </div>
          </div>
        }
      }
    }

    <app-confirm-dialog
      [open]="!!deleteTarget()"
      title="Delete story?"
      [message]="'This permanently removes “' + (deleteTarget()?.title ?? '') + '” and its photos. Its page will stop working.'"
      confirmLabel="Delete"
      [destructive]="true"
      (confirm)="confirmDelete()"
      (cancel)="deleteTarget.set(null)"
    ></app-confirm-dialog>
  `,
})
export class AdminBlogListComponent implements OnInit {
  private readonly blogService = inject(AdminBlogService);
  private readonly toast = inject(ToastService);

  readonly state = signal<LoadState>('loading');
  readonly posts = signal<AdminBlogPostListItem[]>([]);
  readonly deleteTarget = signal<AdminBlogPostListItem | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.blogService.getPosts().subscribe({
      next: (p) => {
        this.posts.set(p);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  confirmDelete(): void {
    const post = this.deleteTarget();
    if (!post) return;
    this.blogService.deletePost(post.blogPostId).subscribe({
      next: () => {
        this.toast.success('Story deleted.');
        this.deleteTarget.set(null);
        this.load();
      },
      error: () => this.deleteTarget.set(null),
    });
  }
}
