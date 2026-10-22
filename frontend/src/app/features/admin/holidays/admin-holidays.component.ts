import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { AdminHolidayService } from '../../../core/services/holiday.service';
import { AdminHolidayDestination, AdminHolidayPackage } from '../../../core/models/holiday.model';
import { ToastService } from '../../../core/services/toast.service';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog.component';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';

type Tab = 'packages' | 'destinations';
type LoadState = 'loading' | 'ready' | 'error';

interface PackageDraft {
  title: string;
  summary: string;
  days: number | null;
  nights: number | null;
  priceFrom: number | null;
  highlights: string;
  holidayDestinationId: number | null;
  displayOrder: number;
  isActive: boolean;
}

interface DestinationDraft {
  name: string;
  tagline: string;
  displayOrder: number;
  isActive: boolean;
}

/**
 * Admin → Holidays: the home page's Holidays tab. Destinations (their own list, separate from trip
 * destinations) and custom holiday packages. No dates or seats — customers enquire on WhatsApp.
 */
@Component({
  selector: 'app-admin-holidays',
  standalone: true,
  imports: [CommonModule, FormsModule, StatePanelComponent, ConfirmDialogComponent, ImageUrlPipe],
  template: `
    <div class="ahead">
      <div>
        <h2>Holidays</h2>
        <div class="sub">Shown on the home page when a visitor picks the Holidays tab. Customers enquire on WhatsApp — there's no online booking for holidays.</div>
      </div>
      <button class="btn sm" (click)="tab() === 'packages' ? newPackage() : newDestination()">{{ tab() === 'packages' ? 'Add package' : 'Add destination' }}</button>
    </div>

    <div class="hol-tabs" role="tablist">
      <button type="button" role="tab" [class.on]="tab() === 'packages'" [attr.aria-selected]="tab() === 'packages'" (click)="switchTab('packages')">Packages <span>{{ packages().length }}</span></button>
      <button type="button" role="tab" [class.on]="tab() === 'destinations'" [attr.aria-selected]="tab() === 'destinations'" (click)="switchTab('destinations')">Destinations <span>{{ destinations().length }}</span></button>
    </div>

    @if (state() === 'loading') {
      <app-state-panel kind="loading"></app-state-panel>
    } @else if (state() === 'error') {
      <app-state-panel kind="error" message="Could not load holidays."></app-state-panel>
    } @else {
      <!-- ---------- Editor ---------- -->
      @if (tab() === 'packages' && pkgDraft) {
        <div class="formsec hol-editor">
          <h4>{{ editingPackageId ? 'Edit package' : 'New package' }}</h4>
          <div class="sub">Add a photo after saving.</div>
          <div class="fld"><span class="lbl">Package name</span><input class="inp" maxlength="150" [(ngModel)]="pkgDraft.title" placeholder="e.g. Goa Beach Holiday" /></div>
          <div class="f3">
            <div class="fld"><span class="lbl">Days</span><input class="inp" type="number" min="1" max="60" [(ngModel)]="pkgDraft.days" /></div>
            <div class="fld"><span class="lbl">Nights</span><input class="inp" type="number" min="0" max="60" [(ngModel)]="pkgDraft.nights" /></div>
            <div class="fld"><span class="lbl">Price from (₹ / person)</span><input class="inp" type="number" min="0" [(ngModel)]="pkgDraft.priceFrom" placeholder="Empty = Price on request" /></div>
          </div>
          <div class="f2">
            <div class="fld">
              <span class="lbl">Destination</span>
              <select class="inp" [(ngModel)]="pkgDraft.holidayDestinationId">
                <option [ngValue]="null">No destination</option>
                @for (d of destinations(); track d.holidayDestinationId) { <option [ngValue]="d.holidayDestinationId">{{ d.name }}</option> }
              </select>
            </div>
            <div class="fld"><span class="lbl">Display order</span><input class="inp" type="number" min="0" [(ngModel)]="pkgDraft.displayOrder" /></div>
          </div>
          <div class="fld"><span class="lbl">Short description</span><textarea class="inp" rows="3" maxlength="1000" [(ngModel)]="pkgDraft.summary"></textarea></div>
          <div class="fld">
            <span class="lbl">Highlights</span>
            <span class="note">One per line — e.g. "3-star stay", "North Goa sightseeing". The first four show on the card.</span>
            <textarea class="inp" rows="4" [(ngModel)]="pkgDraft.highlights"></textarea>
          </div>
          <label class="row" style="gap:6px;margin-bottom:14px"><input type="checkbox" [(ngModel)]="pkgDraft.isActive" /> Show on the website</label>
          @if (formError()) { <div class="errorbox">{{ formError() }}</div> }
          <div class="row" style="gap:8px">
            <button class="btn" [disabled]="saving()" (click)="savePackage()">{{ saving() ? 'Saving…' : 'Save package' }}</button>
            <button class="btn ghost" (click)="cancelEdit()">Cancel</button>
          </div>
        </div>
      }
      @if (tab() === 'destinations' && destDraft) {
        <div class="formsec hol-editor">
          <h4>{{ editingDestinationId ? 'Edit destination' : 'New destination' }}</h4>
          <div class="sub">Shown on the website once it has at least one package that's switched on. Add a photo after saving.</div>
          <div class="f2">
            <div class="fld"><span class="lbl">Name</span><input class="inp" maxlength="100" [(ngModel)]="destDraft.name" placeholder="e.g. Goa" /></div>
            <div class="fld"><span class="lbl">Display order</span><input class="inp" type="number" min="0" [(ngModel)]="destDraft.displayOrder" /></div>
          </div>
          <div class="fld"><span class="lbl">Tagline</span><input class="inp" maxlength="150" [(ngModel)]="destDraft.tagline" placeholder="e.g. Beaches • Nightlife • Forts" /></div>
          <label class="row" style="gap:6px;margin-bottom:14px"><input type="checkbox" [(ngModel)]="destDraft.isActive" /> Show on the website</label>
          @if (formError()) { <div class="errorbox">{{ formError() }}</div> }
          <div class="row" style="gap:8px">
            <button class="btn" [disabled]="saving()" (click)="saveDestination()">{{ saving() ? 'Saving…' : 'Save destination' }}</button>
            <button class="btn ghost" (click)="cancelEdit()">Cancel</button>
          </div>
        </div>
      }

      <!-- ---------- Lists ---------- -->
      @if (tab() === 'packages') {
        @if (packages().length === 0) {
          <app-state-panel kind="empty" message="No holiday packages yet. Add the first one."></app-state-panel>
        } @else {
          <div class="hol-list">
            @for (p of packages(); track p.holidayPackageId) {
              <div class="hol-item" [class.off]="!p.isActive">
                <div class="hol-thumb">
                  @if (p.coverImageUrl) { <img [src]="p.coverImageUrl | imageUrl" [alt]="p.title" /> }
                  <label class="hol-upload">
                    {{ uploadingId() === 'p' + p.holidayPackageId ? 'Uploading…' : (p.coverImageUrl ? 'Change photo' : 'Add photo') }}
                    <input type="file" accept="image/jpeg,image/png,image/webp" (change)="onPackagePhoto(p, $event)" />
                  </label>
                </div>
                <div class="hol-main">
                  <b>{{ p.title }}</b>
                  <span class="mut">{{ p.durationLabel }} · {{ p.destinationName || 'No destination' }} · {{ p.priceFrom !== null ? '₹' + (p.priceFrom | number:'1.0-0') : 'Price on request' }}</span>
                  <span class="badge" [class.ok]="p.isActive" [class.wait]="!p.isActive">{{ p.isActive ? 'On website' : 'Hidden' }}</span>
                </div>
                <div class="hol-acts">
                  <button class="btn sm ghost" (click)="editPackage(p)">Edit</button>
                  <button class="btn sm ghost danger-txt" (click)="confirmDelete.set({ kind: 'package', id: p.holidayPackageId, name: p.title })">Delete</button>
                </div>
              </div>
            }
          </div>
        }
      } @else {
        @if (destinations().length === 0) {
          <app-state-panel kind="empty" message="No holiday destinations yet. Add the first one."></app-state-panel>
        } @else {
          <div class="hol-list">
            @for (d of destinations(); track d.holidayDestinationId) {
              <div class="hol-item" [class.off]="!d.isActive">
                <div class="hol-thumb">
                  @if (d.coverImageUrl) { <img [src]="d.coverImageUrl | imageUrl" [alt]="d.name" /> }
                  <label class="hol-upload">
                    {{ uploadingId() === 'd' + d.holidayDestinationId ? 'Uploading…' : (d.coverImageUrl ? 'Change photo' : 'Add photo') }}
                    <input type="file" accept="image/jpeg,image/png,image/webp" (change)="onDestinationPhoto(d, $event)" />
                  </label>
                </div>
                <div class="hol-main">
                  <b>{{ d.name }}</b>
                  <span class="mut">{{ d.tagline || 'No tagline' }} · {{ d.packageCount }} {{ d.packageCount === 1 ? 'package' : 'packages' }}</span>
                  <span class="badge" [class.ok]="d.isActive" [class.wait]="!d.isActive">{{ d.isActive ? 'On website' : 'Hidden' }}</span>
                </div>
                <div class="hol-acts">
                  <button class="btn sm ghost" (click)="editDestination(d)">Edit</button>
                  <button class="btn sm ghost danger-txt" (click)="confirmDelete.set({ kind: 'destination', id: d.holidayDestinationId, name: d.name })">Delete</button>
                </div>
              </div>
            }
          </div>
        }
      }
    }

    <app-confirm-dialog
      [open]="!!confirmDelete()"
      [title]="confirmDelete()?.kind === 'package' ? 'Delete package?' : 'Delete destination?'"
      [message]="confirmDelete()?.kind === 'package'
        ? 'This removes ' + (confirmDelete()?.name ?? '') + ' from the website.'
        : 'This removes ' + (confirmDelete()?.name ?? '') + '. Its packages stay, without a destination.'"
      confirmLabel="Delete"
      [destructive]="true"
      (confirm)="doDelete()"
      (cancel)="confirmDelete.set(null)"
    ></app-confirm-dialog>
  `,
  styles: `
    .hol-tabs { display: inline-flex; gap: 4px; padding: 4px; margin-bottom: 14px; border-radius: 999px; background: #fff; border: 1px solid var(--line); }
    .hol-tabs button { display: inline-flex; align-items: center; gap: 7px; padding: 8px 16px; border: 0; border-radius: 999px; background: transparent; color: var(--muted); font: inherit; font-size: 13px; font-weight: 600; cursor: pointer; }
    .hol-tabs button span { padding: 1px 8px; border-radius: 999px; background: var(--canvas); font-size: 11.5px; }
    .hol-tabs button.on { background: var(--orange); color: #fff; }
    .hol-tabs button.on span { background: rgba(255,255,255,.25); color: #fff; }
    .hol-editor { border-left: 4px solid var(--orange); }
    .hol-list { display: grid; gap: 10px; }
    .hol-item { display: flex; align-items: center; gap: 14px; padding: 12px; background: #fff; border: 1px solid var(--line); border-radius: var(--radius-card); }
    .hol-item.off { opacity: .7; }
    .hol-thumb { position: relative; flex: none; width: 120px; height: 80px; border-radius: 10px; overflow: hidden; background: linear-gradient(135deg, #CFE7DD, #F6D9C4); }
    .hol-thumb img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .hol-upload { position: absolute; inset: auto 0 0 0; padding: 4px; background: rgba(15,20,22,.62); color: #fff; font-size: 11px; font-weight: 600; text-align: center; cursor: pointer; }
    .hol-upload input { display: none; }
    .hol-main { display: grid; gap: 3px; flex: 1; min-width: 0; }
    .hol-main b { font-size: 14px; }
    .hol-main .mut { font-size: 12px; }
    .hol-main .badge { justify-self: start; }
    .hol-acts { display: flex; gap: 6px; flex-wrap: wrap; justify-content: flex-end; }
    .danger-txt { color: var(--danger); }
    @media (max-width: 640px) {
      .hol-item { flex-wrap: wrap; }
      .hol-thumb { width: 100%; height: 140px; }
      .hol-acts { width: 100%; justify-content: stretch; }
      .hol-acts .btn { flex: 1; }
    }
  `,
})
export class AdminHolidaysComponent implements OnInit {
  private readonly service = inject(AdminHolidayService);
  private readonly toast = inject(ToastService);

  readonly tab = signal<Tab>('packages');
  readonly state = signal<LoadState>('loading');
  readonly packages = signal<AdminHolidayPackage[]>([]);
  readonly destinations = signal<AdminHolidayDestination[]>([]);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly uploadingId = signal<string | null>(null);
  readonly confirmDelete = signal<{ kind: 'package' | 'destination'; id: number; name: string } | null>(null);

  pkgDraft: PackageDraft | null = null;
  editingPackageId: number | null = null;
  destDraft: DestinationDraft | null = null;
  editingDestinationId: number | null = null;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    let pending = 2;
    let failed = false;
    const done = () => {
      if (--pending === 0) this.state.set(failed ? 'error' : 'ready');
    };
    this.service.getPackages().subscribe({ next: (p) => { this.packages.set(p); done(); }, error: () => { failed = true; done(); } });
    this.service.getDestinations().subscribe({ next: (d) => { this.destinations.set(d); done(); }, error: () => { failed = true; done(); } });
  }

  switchTab(tab: Tab): void {
    this.tab.set(tab);
    this.cancelEdit();
  }

  cancelEdit(): void {
    this.pkgDraft = null;
    this.editingPackageId = null;
    this.destDraft = null;
    this.editingDestinationId = null;
    this.formError.set(null);
  }

  // ---------- Packages ----------

  newPackage(): void {
    this.cancelEdit();
    this.pkgDraft = { title: '', summary: '', days: 4, nights: 3, priceFrom: null, highlights: '', holidayDestinationId: null, displayOrder: this.packages().length, isActive: true };
  }

  editPackage(p: AdminHolidayPackage): void {
    this.cancelEdit();
    this.editingPackageId = p.holidayPackageId;
    this.pkgDraft = {
      title: p.title, summary: p.summary ?? '', days: p.days, nights: p.nights, priceFrom: p.priceFrom,
      highlights: p.highlights.join('\n'), holidayDestinationId: p.holidayDestinationId, displayOrder: p.displayOrder, isActive: p.isActive,
    };
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  savePackage(): void {
    const d = this.pkgDraft;
    if (!d) return;
    if (!d.title.trim() || !d.days || d.days < 1 || d.nights === null || d.nights < 0) {
      this.formError.set('Enter the package name, days (1 or more) and nights.');
      return;
    }
    const body = {
      title: d.title.trim(),
      summary: d.summary.trim() || null,
      days: d.days,
      nights: d.nights,
      priceFrom: d.priceFrom === null || (d.priceFrom as unknown) === '' ? null : Number(d.priceFrom),
      highlights: d.highlights.split('\n').map((h) => h.trim()).filter((h) => h.length > 0),
      holidayDestinationId: d.holidayDestinationId,
      displayOrder: Number(d.displayOrder) || 0,
      isActive: d.isActive,
    };
    this.saving.set(true);
    this.formError.set(null);
    const call: Observable<unknown> = this.editingPackageId ? this.service.updatePackage(this.editingPackageId, body) : this.service.createPackage(body);
    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(this.editingPackageId ? 'Holiday package saved.' : 'Holiday package added.');
        this.cancelEdit();
        this.load();
      },
      error: (err: HttpErrorResponse) => this.fail(err, 'Could not save the package.'),
    });
  }

  onPackagePhoto(p: AdminHolidayPackage, event: Event): void {
    this.upload(event, 'p' + p.holidayPackageId, (file) => this.service.setPackagePhoto(p.holidayPackageId, file));
  }

  // ---------- Destinations ----------

  newDestination(): void {
    this.cancelEdit();
    this.destDraft = { name: '', tagline: '', displayOrder: this.destinations().length, isActive: true };
  }

  editDestination(d: AdminHolidayDestination): void {
    this.cancelEdit();
    this.editingDestinationId = d.holidayDestinationId;
    this.destDraft = { name: d.name, tagline: d.tagline ?? '', displayOrder: d.displayOrder, isActive: d.isActive };
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  saveDestination(): void {
    const d = this.destDraft;
    if (!d) return;
    if (!d.name.trim()) {
      this.formError.set('Enter the destination name.');
      return;
    }
    const body = { name: d.name.trim(), tagline: d.tagline.trim() || null, displayOrder: Number(d.displayOrder) || 0, isActive: d.isActive };
    this.saving.set(true);
    this.formError.set(null);
    const call: Observable<unknown> = this.editingDestinationId ? this.service.updateDestination(this.editingDestinationId, body) : this.service.createDestination(body);
    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.toast.success(this.editingDestinationId ? 'Holiday destination saved.' : 'Holiday destination added.');
        this.cancelEdit();
        this.load();
      },
      error: (err: HttpErrorResponse) => this.fail(err, 'Could not save the destination.'),
    });
  }

  onDestinationPhoto(d: AdminHolidayDestination, event: Event): void {
    this.upload(event, 'd' + d.holidayDestinationId, (file) => this.service.setDestinationPhoto(d.holidayDestinationId, file));
  }

  // ---------- Shared ----------

  doDelete(): void {
    const target = this.confirmDelete();
    if (!target) return;
    this.confirmDelete.set(null);
    const call: Observable<unknown> = target.kind === 'package' ? this.service.deletePackage(target.id) : this.service.deleteDestination(target.id);
    call.subscribe({
      next: () => {
        this.toast.success(target.kind === 'package' ? 'Holiday package deleted.' : 'Holiday destination deleted.');
        this.cancelEdit();
        this.load();
      },
    });
  }

  private upload(event: Event, key: string, send: (file: File) => ReturnType<AdminHolidayService['setPackagePhoto']>): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.uploadingId.set(key);
    send(file).subscribe({
      next: () => {
        this.uploadingId.set(null);
        this.toast.success('Photo updated.');
        this.load();
      },
      error: () => this.uploadingId.set(null),
    });
  }

  private fail(err: HttpErrorResponse, fallback: string): void {
    this.saving.set(false);
    this.formError.set(err.error?.errors?.join(' ') || err.error?.message || fallback);
  }
}
