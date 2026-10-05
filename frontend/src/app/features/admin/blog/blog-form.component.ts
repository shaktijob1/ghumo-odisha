import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminBlogService } from '../../../core/services/admin-blog.service';
import { AdminBlogPostDetail, SaveBlogPostRequest } from '../../../core/models/blog.model';
import { ToastService } from '../../../core/services/toast.service';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';

/** Most extra photos one story holds (IBlogService.MaxPhotosPerPost on the API). */
const MAX_PHOTOS = 12;

/** "a, b\nc" → ["a", "b", "c"]: one tag per line, commas split too, repeats dropped. */
function parseTags(text: string): string[] {
  const seen = new Set<string>();
  return text
    .split(/[\n,]/)
    .map((t) => t.trim())
    .filter((t) => {
      const key = t.toLowerCase();
      if (!t || seen.has(key)) return false;
      seen.add(key);
      return true;
    });
}

/** Admin → Stories → write / edit one story, its hero photo, extra photos and popular tags. */
@Component({
  selector: 'app-admin-blog-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, StatePanelComponent, ImageUrlPipe],
  template: `
    @if (loading()) {
      <app-state-panel kind="loading"></app-state-panel>
    } @else {
      <div class="ahead">
        <div>
          <h2>{{ postId ? 'Edit story' : 'Write a story' }}</h2>
          <div class="sub">{{ postId ? 'Changes show on the site as soon as you save.' : 'Write the story, create it, then add the hero photo and more photos.' }}</div>
        </div>
        <div class="row" style="gap:8px">
          @if (post()?.isPublished) {
            <a class="btn sm ghost" [href]="'/blog/' + post()!.slug" target="_blank" rel="noopener">View on site</a>
          }
          <a class="btn sm ghost" routerLink="/admin/stories">Back to stories</a>
        </div>
      </div>

      <form [formGroup]="form" (ngSubmit)="save()">
        <div class="formsec">
          <h4>Story</h4>
          <div class="sub">The title is the page heading; the slug forms the address, e.g. /blog/best-places-to-visit-in-koraput.</div>

          <div class="fld">
            <span class="lbl">Title</span>
            <input class="inp" formControlName="title" placeholder="e.g. Best Places to Visit in Koraput" />
          </div>
          <div class="f2">
            <div class="fld">
              <span class="lbl">Slug (URL)</span>
              <input class="inp" formControlName="slug" />
              @if (form.controls.slug.touched && form.controls.slug.invalid) {
                <span class="note" style="color:var(--danger)">Lowercase letters, numbers and hyphens only.</span>
              }
            </div>
            <div class="fld">
              <span class="lbl">Place</span>
              <input class="inp" formControlName="place" placeholder="e.g. Mahendragiri" />
            </div>
          </div>
          <div class="fld">
            <span class="lbl">Short summary</span>
            <textarea class="inp" formControlName="excerpt" rows="2" maxlength="500" placeholder="One or two sentences — shown on story cards and in Google results."></textarea>
            <span class="note">{{ form.controls.excerpt.value.length }} / 500 · about 150 characters reads best in Google.</span>
          </div>
          <div class="fld">
            <span class="lbl">Story text</span>
            <textarea class="inp" formControlName="content" rows="18" style="font-family:inherit;line-height:1.6"></textarea>
            <span class="note">Leave a blank line between paragraphs. Start a line with <b>##</b> for a heading (e.g. "## 1. Deomali") and with <b>-</b> for a bullet point.</span>
          </div>
          <label class="row" style="gap:6px;margin-top:6px"><input type="checkbox" formControlName="isPublished" /> Published (visible on the website)</label>
        </div>

        <div class="formsec">
          <h4>Popular tags</h4>
          <div class="sub">The phrases people search for — shown under "Popular tags" on the story and given to search engines. One per line (commas work too).</div>
          <textarea class="inp" formControlName="tags" rows="8" placeholder="Best places to visit in Koraput&#10;Koraput tourist places&#10;Koraput trip from Bhubaneswar"></textarea>
          <span class="note">{{ tagCount() }} tag{{ tagCount() === 1 ? '' : 's' }} · up to 120</span>

          <div style="margin-top:14px">
            <button class="btn" type="submit" [disabled]="saving()">{{ postId ? 'Save changes' : 'Create story' }}</button>
          </div>
        </div>
      </form>

      @if (postId) {
        <div class="formsec">
          <h4>Hero photo</h4>
          <div class="sub">The large banner at the top of the story and the photo on its card. Landscape photos work best.</div>
          <label class="drop">
            {{ uploadingHero() ? 'Uploading…' : 'Click to choose an image (JPG, PNG or WebP, up to 5 MB)' }}
            <input type="file" accept="image/jpeg,image/png,image/webp" style="display:none" [disabled]="uploadingHero()" (change)="onHeroSelected($event)" />
          </label>
          @if (post()?.heroImageUrl; as url) {
            <div class="thumbs"><div class="t"><img [src]="url | imageUrl" alt="" /></div></div>
          }
        </div>

        <div class="formsec">
          <h4>More photos ({{ post()?.photos?.length ?? 0 }} / {{ maxPhotos }})</h4>
          <div class="sub">Shown in a photo strip under the hero; visitors can tap to view them full screen. Add a caption first if you like (e.g. "Kunti temple at the summit").</div>
          <div class="f2" style="align-items:end">
            <div class="fld">
              <span class="lbl">Caption for the next photo (optional)</span>
              <input class="inp" #cap maxlength="150" />
            </div>
            <label class="btn ghost" style="cursor:pointer;justify-content:center" [class.disabled]="uploadingPhoto() || (post()?.photos?.length ?? 0) >= maxPhotos">
              {{ uploadingPhoto() ? 'Uploading…' : 'Add photos' }}
              <input type="file" accept="image/jpeg,image/png,image/webp" multiple style="display:none"
                [disabled]="uploadingPhoto() || (post()?.photos?.length ?? 0) >= maxPhotos" (change)="onPhotosSelected($event, cap)" />
            </label>
          </div>
          @if (post()?.photos?.length) {
            <div class="thumbs">
              @for (ph of post()!.photos; track ph.blogPostPhotoId) {
                <div class="t" [title]="ph.caption ?? ''">
                  <img [src]="ph.imageUrl | imageUrl" [alt]="ph.caption ?? ''" />
                  <button type="button" class="x" aria-label="Delete photo" (click)="deletePhoto(ph.blogPostPhotoId)">✕</button>
                </div>
              }
            </div>
          }
        </div>
      }
    }
  `,
})
export class AdminBlogFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly blogService = inject(AdminBlogService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly maxPhotos = MAX_PHOTOS;
  postId: number | null = null;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly uploadingHero = signal(false);
  readonly uploadingPhoto = signal(false);
  readonly post = signal<AdminBlogPostDetail | null>(null);
  private slugTouched = false;

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(200)]],
    slug: ['', [Validators.required, Validators.maxLength(200), Validators.pattern('^[a-z0-9]+(-[a-z0-9]+)*$')]],
    place: ['', Validators.maxLength(100)],
    excerpt: ['', [Validators.required, Validators.maxLength(500)]],
    content: ['', Validators.required],
    tags: [''],
    isPublished: [true],
  });

  private readonly tagsText = toSignal(this.form.controls.tags.valueChanges, { initialValue: '' });
  readonly tagCount = computed(() => parseTags(this.tagsText()).length);

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.postId = idParam ? Number(idParam) : null;

    this.form.controls.title.valueChanges.subscribe((value) => {
      if (this.slugTouched) return;
      const slug = value.toLowerCase().trim().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');
      this.form.patchValue({ slug }, { emitEvent: false });
    });
    this.form.controls.slug.valueChanges.subscribe(() => (this.slugTouched = true));

    if (this.postId) {
      this.slugTouched = true;
      this.load(this.postId);
    } else {
      this.loading.set(false);
    }
  }

  load(id: number): void {
    this.blogService.getPost(id).subscribe({
      next: (p) => {
        this.post.set(p);
        this.form.patchValue(
          {
            title: p.title,
            slug: p.slug,
            place: p.place ?? '',
            excerpt: p.excerpt,
            content: p.content,
            tags: p.tags.join('\n'),
            isPublished: p.isPublished,
          },
          { emitEvent: false },
        );
        this.form.controls.tags.setValue(p.tags.join('\n'));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const request: SaveBlogPostRequest = {
      title: v.title.trim(),
      slug: v.slug.trim(),
      place: v.place.trim() || null,
      excerpt: v.excerpt.trim(),
      content: v.content,
      tags: parseTags(v.tags),
      isPublished: v.isPublished,
    };

    this.saving.set(true);
    if (this.postId) {
      this.blogService.updatePost(this.postId, request).subscribe({
        next: () => {
          this.saving.set(false);
          this.toast.success('Story saved.');
          this.load(this.postId!);
        },
        error: () => this.saving.set(false),
      });
    } else {
      this.blogService.createPost(request).subscribe({
        next: (res) => {
          this.saving.set(false);
          this.toast.success('Story created. Now add a hero photo.');
          this.router.navigate(['/admin/stories', res.blogPostId, 'edit']);
        },
        error: () => this.saving.set(false),
      });
    }
  }

  onHeroSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.postId) return;

    this.uploadingHero.set(true);
    this.blogService.setHeroImage(this.postId, file).subscribe({
      next: () => {
        this.uploadingHero.set(false);
        this.toast.success('Hero photo updated.');
        this.load(this.postId!);
      },
      error: () => this.uploadingHero.set(false),
    });
    input.value = '';
  }

  /** Uploads the chosen photos one after another (the caption goes with the first). */
  onPhotosSelected(event: Event, captionInput: HTMLInputElement): void {
    const input = event.target as HTMLInputElement;
    const room = MAX_PHOTOS - (this.post()?.photos.length ?? 0);
    const files = Array.from(input.files ?? []).slice(0, Math.max(0, room));
    input.value = '';
    if (!files.length || !this.postId) return;
    if ((input.files?.length ?? 0) > room) this.toast.error(`Only ${room} more photo${room === 1 ? '' : 's'} fit — the rest were skipped.`);

    const caption = captionInput.value;
    this.uploadingPhoto.set(true);
    const next = (i: number): void => {
      if (i >= files.length) {
        this.uploadingPhoto.set(false);
        captionInput.value = '';
        this.toast.success(files.length === 1 ? 'Photo added.' : `${files.length} photos added.`);
        this.load(this.postId!);
        return;
      }
      this.blogService.addPhoto(this.postId!, files[i], i === 0 ? caption : undefined).subscribe({
        next: () => next(i + 1),
        error: () => {
          this.uploadingPhoto.set(false);
          this.load(this.postId!);
        },
      });
    };
    next(0);
  }

  deletePhoto(photoId: number): void {
    if (!this.postId) return;
    this.blogService.deletePhoto(this.postId, photoId).subscribe({
      next: () => {
        this.toast.success('Photo deleted.');
        this.load(this.postId!);
      },
    });
  }
}
