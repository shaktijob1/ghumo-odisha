import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { AdminOrganizerService, OrganizerProfile, OrganizerProfileFields } from '../../../core/services/admin-organizer.service';
import { ToastService } from '../../../core/services/toast.service';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';

type Field = { key: keyof OrganizerProfileFields; label: string; max: number; wide?: boolean; multiline?: boolean; type?: string };

/**
 * Admin dashboard: the home page's "Travel Organizer" card — photo, name, role, contact details,
 * Instagram link and the card's texts. Any field left empty shows the default (placeholder).
 * The WhatsApp number isn't here: it's in appsettings, tied to the WhatsApp Business setup.
 */
@Component({
  selector: 'app-organizer-panel',
  standalone: true,
  imports: [FormsModule, ImageUrlPipe],
  template: `
    <div class="panel org-panel">
      <h4>Travel Organizer</h4>
      <div class="sub">The contact card on the customer home page and the corner Call / WhatsApp / Instagram buttons. Leave a field empty to use the default shown in grey.</div>

      @if (state() === 'loading') {
        <p class="note">Loading…</p>
      } @else if (state() === 'error') {
        <p class="note">Could not load the organizer details. <button type="button" class="linkbtn" (click)="load()">Try again</button></p>
      } @else {
        <div class="org-grid">
          <div class="org-photo">
            @if (photoUrl()) {
              <img [src]="photoUrl()! | imageUrl" alt="Organizer" />
            } @else {
              <div class="org-ph" aria-hidden="true"></div>
            }
            <label class="btn sm ghost" style="cursor:pointer">
              @if (uploading()) { <span class="spin"></span> } @else { {{ photoUrl() ? 'Change photo' : 'Upload photo' }} }
              <input type="file" accept="image/jpeg,image/png,image/webp" style="display:none" (change)="onPhotoSelected($event)" />
            </label>
          </div>

          <div class="org-fields">
            @for (f of fields; track f.key) {
              <div class="fld" [class.wide]="f.wide">
                <span class="lbl">{{ f.label }}</span>
                @if (f.multiline) {
                  <textarea class="inp" rows="2" [maxlength]="f.max" [placeholder]="placeholder(f.key)" [(ngModel)]="form[f.key]"></textarea>
                } @else {
                  <input class="inp" [type]="f.type ?? 'text'" [maxlength]="f.max" [placeholder]="placeholder(f.key)" [(ngModel)]="form[f.key]" />
                }
              </div>
            }
            <p class="note wide">WhatsApp number: <b>{{ defaults()?.whatsAppNumber || '—' }}</b> (set in appsettings OrganizerContact:WhatsAppNumber).</p>
            @if (error()) { <div class="errorbox wide">{{ error() }}</div> }
            <div class="wide"><button type="button" class="btn" [disabled]="saving()" (click)="save()">{{ saving() ? 'Saving…' : 'Save organizer details' }}</button></div>
          </div>
        </div>
      }
    </div>
  `,
  styles: `
    .org-panel { margin-bottom: 16px; }
    .org-grid { display: grid; grid-template-columns: 150px 1fr; gap: 18px; margin-top: 12px; }
    .org-photo { display: flex; flex-direction: column; align-items: center; gap: 10px; }
    .org-photo img, .org-ph { width: 120px; height: 120px; border-radius: 50%; object-fit: cover; }
    .org-ph { background: var(--canvas); border: 1px dashed var(--line); }
    .org-fields { display: grid; grid-template-columns: 1fr 1fr; gap: 12px 14px; }
    .org-fields .fld { margin: 0; }
    .org-fields .wide { grid-column: 1 / -1; }
    .linkbtn { border: 0; background: none; padding: 0; color: var(--accent); font: inherit; font-weight: 600; cursor: pointer; }
    @media (max-width: 760px) {
      .org-grid { grid-template-columns: 1fr; }
      .org-fields { grid-template-columns: 1fr; }
    }
  `,
})
export class OrganizerPanelComponent implements OnInit {
  private readonly organizer = inject(AdminOrganizerService);
  private readonly toast = inject(ToastService);

  readonly fields: Field[] = [
    { key: 'name', label: 'Name', max: 100 },
    { key: 'role', label: 'Role', max: 100 },
    { key: 'phone', label: 'Phone (Call button)', max: 20, type: 'tel' },
    { key: 'email', label: 'Email', max: 200, type: 'email' },
    { key: 'instagramUrl', label: 'Instagram link', max: 300, wide: true, type: 'url' },
    { key: 'quote', label: 'Quote', max: 300, wide: true, multiline: true },
    { key: 'photoCaption', label: 'Caption on the photo', max: 100 },
    { key: 'helpTitle', label: 'Help heading', max: 100 },
    { key: 'helpText', label: 'Help line', max: 200, wide: true },
  ];

  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly saving = signal(false);
  readonly uploading = signal(false);
  readonly error = signal<string | null>(null);
  readonly photoUrl = signal<string | null>(null);
  readonly defaults = signal<OrganizerProfile['defaults'] | null>(null);

  form: Record<keyof OrganizerProfileFields, string> = {
    name: '', role: '', phone: '', email: '', instagramUrl: '', quote: '', photoCaption: '', helpTitle: '', helpText: '',
  };

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.organizer.getProfile().subscribe({
      next: (p) => {
        this.photoUrl.set(p.photoUrl);
        this.defaults.set(p.defaults);
        for (const f of this.fields) this.form[f.key] = p[f.key] ?? '';
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  placeholder(key: keyof OrganizerProfileFields): string {
    return (this.defaults()?.[key] as string | null | undefined) ?? '';
  }

  save(): void {
    this.error.set(null);
    this.saving.set(true);
    const body = Object.fromEntries(this.fields.map((f) => [f.key, this.form[f.key].trim() || null])) as unknown as OrganizerProfileFields;
    this.organizer.updateProfile(body).subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success('Travel Organizer details saved.');
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.error.set(err.error?.errors?.join(' ') || err.error?.message || 'Could not save the organizer details.');
      },
    });
  }

  onPhotoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.uploading.set(true);
    this.organizer.setPhoto(file).subscribe({
      next: () => {
        this.uploading.set(false);
        this.toast.success('Organizer photo updated.');
        this.load();
      },
      error: () => this.uploading.set(false),
    });
  }
}
