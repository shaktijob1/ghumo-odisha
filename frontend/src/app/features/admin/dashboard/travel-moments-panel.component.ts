import { Component, OnInit, inject, signal } from '@angular/core';
import { AdminBlogService } from '../../../core/services/admin-blog.service';
import { BlogService } from '../../../core/services/blog.service';
import { ToastService } from '../../../core/services/toast.service';
import { MAX_TRAVEL_MOMENTS, TravelMoment } from '../../../core/models/blog.model';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';

/** Admin dashboard: upload, caption, reorder and delete the home page's "Real Travel Moments" photos. */
@Component({
  selector: 'app-travel-moments-panel',
  standalone: true,
  imports: [ImageUrlPipe],
  template: `
    <div class="panel" style="margin-bottom:16px">
      <div class="row sp" style="align-items:flex-start;gap:12px;flex-wrap:wrap">
        <div>
          <h4>Real Travel Moments ({{ moments().length }} / {{ max }})</h4>
          <div class="sub">Real photos from your trips, shown in a gallery on the customer homepage. Add 5 to 10 for the best look; the first photo is shown largest.</div>
        </div>
        <label class="btn sm" style="cursor:pointer" [class.disabled]="uploading() || moments().length >= max">
          @if (uploading()) { <span class="spin"></span> Uploading… } @else { Add photos }
          <input type="file" accept="image/jpeg,image/png,image/webp" multiple style="display:none"
            [disabled]="uploading() || moments().length >= max" (change)="onFilesSelected($event)" />
        </label>
      </div>

      @if (loadFailed()) {
        <p class="note" style="margin-top:10px">Could not load the photos. <a href="" (click)="$event.preventDefault(); load()">Try again</a></p>
      } @else if (moments().length === 0) {
        <p class="note" style="margin-top:10px">No photos yet — the section stays hidden on the homepage until you add one.</p>
      } @else {
        @if (moments().length < 5) {
          <p class="note" style="margin-top:10px;color:var(--wait)">Add {{ 5 - moments().length }} more for a fuller gallery (5 to 10 recommended).</p>
        }
        <div class="tmp-grid">
          @for (m of moments(); track m.travelMomentId; let i = $index, first = $first, last = $last) {
            <div class="tmp-item">
              <div class="tmp-ph">
                <img [src]="m.imageUrl | imageUrl" [alt]="m.caption ?? ''" />
                @if (first) { <span class="tmp-big">Large</span> }
              </div>
              <input class="inp tmp-cap" maxlength="150" placeholder="Caption (optional)" [value]="m.caption ?? ''"
                (change)="saveCaption(m, $any($event.target).value)" />
              <div class="tmp-acts">
                <button type="button" class="sel" [disabled]="first || busy()" (click)="move(m, -1)" aria-label="Move earlier">←</button>
                <button type="button" class="sel" [disabled]="last || busy()" (click)="move(m, 1)" aria-label="Move later">→</button>
                <button type="button" class="sel" style="color:var(--danger);margin-left:auto" [disabled]="busy()" (click)="remove(m)">Delete</button>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: `
    .tmp-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); gap: 12px; margin-top: 12px; }
    .tmp-item { border: 1px solid var(--line); border-radius: 12px; padding: 8px; background: #fff; display: flex; flex-direction: column; gap: 6px; }
    .tmp-ph { position: relative; aspect-ratio: 4 / 3; border-radius: 8px; overflow: hidden; background: var(--canvas); }
    .tmp-ph img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .tmp-big { position: absolute; left: 6px; top: 6px; background: var(--accent); color: #fff; font-size: 10px; font-weight: 700; padding: 2px 7px; border-radius: 999px; }
    .tmp-cap { font-size: 12px; padding: 6px 8px; }
    .tmp-acts { display: flex; gap: 5px; }
    .tmp-acts .sel { padding: 4px 9px; cursor: pointer; }
    .tmp-acts .sel:disabled { opacity: .45; cursor: default; }
    label.disabled { opacity: .55; pointer-events: none; }
  `,
})
export class TravelMomentsPanelComponent implements OnInit {
  private readonly adminService = inject(AdminBlogService);
  private readonly publicService = inject(BlogService);
  private readonly toast = inject(ToastService);

  readonly max = MAX_TRAVEL_MOMENTS;
  readonly moments = signal<TravelMoment[]>([]);
  readonly loadFailed = signal(false);
  readonly uploading = signal(false);
  readonly busy = signal(false);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.publicService.getTravelMoments().subscribe({
      next: (m) => {
        this.moments.set(m);
        this.loadFailed.set(false);
      },
      error: () => this.loadFailed.set(true),
    });
  }

  /** Uploads the chosen photos one after another, up to the 10-photo limit. */
  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const room = this.max - this.moments().length;
    const chosen = Array.from(input.files ?? []);
    const files = chosen.slice(0, Math.max(0, room));
    input.value = '';
    if (!files.length) return;
    if (chosen.length > room) this.toast.error(`Only ${room} more photo${room === 1 ? '' : 's'} fit (limit ${this.max}) — the rest were skipped.`);

    this.uploading.set(true);
    let added = 0;
    const next = (i: number): void => {
      if (i >= files.length) {
        this.uploading.set(false);
        if (added) this.toast.success(added === 1 ? 'Photo added.' : `${added} photos added.`);
        return;
      }
      this.adminService.addMoment(files[i]).subscribe({
        next: (m) => {
          added++;
          this.moments.update((list) => [...list, m]);
          next(i + 1);
        },
        error: () => {
          this.uploading.set(false);
          this.load();
        },
      });
    };
    next(0);
  }

  saveCaption(m: TravelMoment, value: string): void {
    const caption = value.trim() || null;
    if (caption === m.caption) return;
    this.adminService.updateMomentCaption(m.travelMomentId, caption).subscribe({
      next: () => {
        this.moments.update((list) => list.map((x) => (x.travelMomentId === m.travelMomentId ? { ...x, caption } : x)));
        this.toast.success('Caption saved.');
      },
    });
  }

  move(m: TravelMoment, direction: -1 | 1): void {
    this.busy.set(true);
    this.adminService.moveMoment(m.travelMomentId, direction).subscribe({
      next: () => {
        this.busy.set(false);
        this.load();
      },
      error: () => this.busy.set(false),
    });
  }

  remove(m: TravelMoment): void {
    if (!confirm('Delete this photo from Real Travel Moments?')) return;
    this.busy.set(true);
    this.adminService.deleteMoment(m.travelMomentId).subscribe({
      next: () => {
        this.busy.set(false);
        this.moments.update((list) => list.filter((x) => x.travelMomentId !== m.travelMomentId));
        this.toast.success('Photo deleted.');
      },
      error: () => this.busy.set(false),
    });
  }
}
