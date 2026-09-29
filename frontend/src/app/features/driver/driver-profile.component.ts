import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApprovalLabels, DriverDocumentType, DriverDocumentTypeLabels, DriverStatus, approvalBadgeClass } from '../../core/models/car.model';
import { DriverDocument, DriverProfile } from '../../core/models/driver.model';
import { DriverAuthService } from '../../core/services/driver-auth.service';
import { DriverService } from '../../core/services/driver.service';
import { ToastService } from '../../core/services/toast.service';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { apiErrorMessage } from '../../shared/utils/api-error';
import { istDate } from '../../shared/utils/car-format';

/** /driver/profile — personal details, photo, WhatsApp number, licence and documents; send for approval. */
@Component({
  selector: 'app-driver-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, ImageUrlPipe, StatePanelComponent],
  template: `
    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') {
        <div class="card pad cz-empty"><p>{{ error() }}</p><button type="button" class="btn ghost sm" (click)="load()">Try again</button></div>
      }
      @default {
        @if (profile(); as p) {
          <div class="cz-cardhead" style="margin-bottom:14px">
            <h1 class="dv-h1" style="margin:0">Profile</h1>
            <span class="badge" [class]="badge()">{{ statusLabel() }}</span>
          </div>
          @if (p.statusReason && (p.status === Status.Rejected || p.status === Status.Suspended)) {
            <div class="card pad cz-alert" style="margin-bottom:14px"><b>Note from Ghumo Odisha</b><span>{{ p.statusReason }}</span></div>
          }

          <section class="card pad dv-photo">
            <div class="dv-avatar">
              @if (photoPreview() || p.profilePhotoUrl) {
                <img [src]="photoPreview() || (p.profilePhotoUrl | imageUrl)" alt="Your photo" />
              } @else {
                <span>{{ initial() }}</span>
              }
            </div>
            <div>
              <b>Profile photo</b>
              <p class="note">A clear face photo. Customers see it once they book you.</p>
              <label class="btn ghost sm dv-file">
                @if (photoBusy()) { <span class="spin"></span> } @else { {{ p.profilePhotoUrl ? 'Change photo' : 'Add photo' }} }
                <input type="file" accept="image/jpeg,image/png,image/webp" capture="user" (change)="onPhoto($event)" [disabled]="photoBusy()" />
              </label>
            </div>
          </section>

          <section class="card pad">
            <h3 class="cz-h3">Your details</h3>
            <form (ngSubmit)="save()">
              <div class="f2 dv-f2">
                <div class="fld"><label class="lbl" for="p-name">Full name</label><input id="p-name" class="inp" name="name" [(ngModel)]="form.name" maxlength="150" required /></div>
                <div class="fld"><label class="lbl" for="p-email">Email <span class="mut">(optional)</span></label><input id="p-email" class="inp" type="email" name="email" [(ngModel)]="form.email" maxlength="200" /></div>
              </div>
              <div class="fld"><label class="lbl" for="p-addr">Address</label><textarea id="p-addr" class="inp" rows="2" name="address" [(ngModel)]="form.address" maxlength="500"></textarea></div>
              <div class="f2 dv-f2">
                <div class="fld"><label class="lbl" for="p-city">City</label><input id="p-city" class="inp" name="city" [(ngModel)]="form.city" maxlength="100" /></div>
                <div class="fld"><label class="lbl" for="p-exp">Driving experience (years)</label><input id="p-exp" class="inp" type="number" min="0" max="60" name="exp" [(ngModel)]="form.experienceYears" /></div>
              </div>
              <div class="f2 dv-f2">
                <div class="fld"><label class="lbl" for="p-dl">Driving licence number</label><input id="p-dl" class="inp" name="dl" [(ngModel)]="form.drivingLicenceNumber" maxlength="25" placeholder="e.g. OD02 20190012345" /></div>
                <div class="fld"><label class="lbl" for="p-dlx">Licence valid until</label><input id="p-dlx" class="inp" type="date" name="dlx" [(ngModel)]="form.licenceExpiryDate" /></div>
              </div>
              @if (p.status === Status.Approved) {
                <p class="note" style="margin-bottom:10px">Changing your name or licence number sends your profile for approval again.</p>
              }
              @if (saveError()) { <div class="errorbox">{{ saveError() }}</div> }
              <button type="submit" class="btn" [disabled]="saving()">@if (saving()) { <span class="spin"></span> } @else { Save details }</button>
            </form>
          </section>

          <section class="card pad">
            <h3 class="cz-h3">WhatsApp number</h3>
            @if (p.phoneNumber && !changingPhone()) {
              <div class="row"><b>+91 {{ p.phoneNumber }}</b><span class="badge ok">Verified</span>
                <button type="button" class="linkbtn" (click)="changingPhone.set(true)">Change</button></div>
            } @else {
              <p class="note" style="margin-bottom:10px">Customers and our team reach you here. We'll send a code to confirm it.</p>
              @if (!phoneOtpSent()) {
                <div class="row wrap">
                  <div class="inp numprefix" style="flex:1;min-width:200px"><span class="numprefix-code">+91</span>
                    <input type="tel" inputmode="numeric" maxlength="10" placeholder="10-digit number" [(ngModel)]="newPhone" name="newPhone" /></div>
                  <button type="button" class="btn sm" [disabled]="phoneBusy() || newPhone.length !== 10" (click)="sendPhoneOtp()">Send OTP</button>
                </div>
              } @else {
                <div class="row wrap">
                  <input class="inp" style="flex:1;min-width:140px" inputmode="numeric" autocomplete="one-time-code" maxlength="8" placeholder="OTP" [(ngModel)]="phoneOtp" name="phoneOtp" />
                  <button type="button" class="btn sm" [disabled]="phoneBusy() || !phoneOtp" (click)="verifyPhone()">Verify</button>
                </div>
              }
              @if (phoneError()) { <div class="errorbox" style="margin-top:10px">{{ phoneError() }}</div> }
            }
          </section>

          <section class="card pad">
            <h3 class="cz-h3">Documents</h3>
            <p class="note" style="margin-bottom:12px">Only you and Ghumo Odisha can see these. A photo of your driving licence is required.</p>
            @for (d of p.documents; track d.driverDocumentId) {
              <div class="dv-doc">
                <div><b>{{ docLabel(d.documentType) }}</b><span class="mut">Uploaded {{ date(d.createdAt) }}@if (d.carId) { · car document }</span></div>
                <button type="button" class="btn ghost sm" (click)="view(d)">View</button>
                @if (p.status !== Status.Approved) {
                  <button type="button" class="btn dang sm" (click)="removeDoc(d)">Remove</button>
                }
              </div>
            }
            <div class="row wrap" style="margin-top:12px">
              <select class="inp" style="flex:1;min-width:180px" [(ngModel)]="docType" name="docType">
                @for (t of docTypes; track t.value) { <option [ngValue]="t.value">{{ t.label }}</option> }
              </select>
              <label class="btn sm dv-file">
                @if (docBusy()) { <span class="spin"></span> } @else { Upload }
                <input type="file" accept="image/jpeg,image/png,image/webp,application/pdf" (change)="onDoc($event)" [disabled]="docBusy()" />
              </label>
            </div>
            @if (docError()) { <div class="errorbox" style="margin-top:10px">{{ docError() }}</div> }
          </section>

          @if (p.status === Status.Pending && !p.submittedForReviewAt || p.status === Status.Rejected) {
            <section class="card pad">
              <h3 class="cz-h3">Send for approval</h3>
              @if (p.missingForReview.length) {
                <ul class="dv-todo">@for (m of p.missingForReview; track m) { <li>{{ m }}</li> }</ul>
              } @else {
                <p class="mut">Everything's in. Send your profile to our team.</p>
              }
              @if (submitError()) { <div class="errorbox" style="margin-top:10px">{{ submitError() }}</div> }
              <button type="button" class="btn" style="margin-top:12px" [disabled]="p.missingForReview.length > 0 || submitting()" (click)="submit()">
                @if (submitting()) { <span class="spin"></span> } @else { Send for approval }
              </button>
            </section>
          }
        }
      }
    }
  `,
})
export class DriverProfileComponent implements OnInit {
  private readonly driverService = inject(DriverService);
  private readonly auth = inject(DriverAuthService);
  private readonly toast = inject(ToastService);

  readonly Status = DriverStatus;
  readonly state = signal<'loading' | 'ready' | 'error'>('loading');
  readonly error = signal('');
  readonly profile = signal<DriverProfile | null>(null);

  form = { name: '', email: '', address: '', city: '', drivingLicenceNumber: '', licenceExpiryDate: '', experienceYears: null as number | null };
  readonly saving = signal(false);
  readonly saveError = signal<string | null>(null);

  readonly photoPreview = signal<string | null>(null);
  readonly photoBusy = signal(false);

  readonly changingPhone = signal(false);
  readonly phoneOtpSent = signal(false);
  readonly phoneBusy = signal(false);
  readonly phoneError = signal<string | null>(null);
  newPhone = '';
  phoneOtp = '';

  readonly docTypes = [DriverDocumentType.DrivingLicence, DriverDocumentType.Other].map((value) => ({ value, label: DriverDocumentTypeLabels[value] }));
  docType = DriverDocumentType.DrivingLicence;
  readonly docBusy = signal(false);
  readonly docError = signal<string | null>(null);

  readonly submitting = signal(false);
  readonly submitError = signal<string | null>(null);

  readonly initial = computed(() => (this.profile()?.name || 'D').charAt(0).toUpperCase());
  readonly statusLabel = computed(() => {
    const p = this.profile();
    if (!p) return '';
    return p.status === DriverStatus.Pending && !p.submittedForReviewAt ? 'Profile incomplete' : ApprovalLabels.driver[p.status];
  });
  readonly badge = computed(() => approvalBadgeClass(this.profile()?.status ?? 0, DriverStatus.Approved, [DriverStatus.Rejected, DriverStatus.Suspended]));
  readonly date = istDate;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.driverService.profile().subscribe({
      next: (p) => {
        this.apply(p);
        this.state.set('ready');
      },
      error: (e: unknown) => {
        this.error.set(apiErrorMessage(e, 'Could not load your profile.'));
        this.state.set('error');
      },
    });
  }

  save(): void {
    this.saving.set(true);
    this.saveError.set(null);
    const f = this.form;
    this.driverService
      .updateProfile({
        name: f.name.trim(),
        email: f.email.trim() || null,
        address: f.address.trim() || null,
        city: f.city.trim() || null,
        drivingLicenceNumber: f.drivingLicenceNumber.trim() || null,
        licenceExpiryDate: f.licenceExpiryDate || null,
        experienceYears: f.experienceYears ?? null,
      })
      .subscribe({
        next: (p) => {
          this.apply(p);
          this.saving.set(false);
          this.toast.success('Profile saved.');
        },
        error: (e: unknown) => {
          this.saving.set(false);
          this.saveError.set(apiErrorMessage(e));
        },
      });
  }

  onPhoto(event: Event): void {
    const file = this.takeFile(event);
    if (!file) return;
    this.photoPreview.set(URL.createObjectURL(file));
    this.photoBusy.set(true);
    this.driverService.setProfilePhoto(file).subscribe({
      next: (p) => {
        this.apply(p, false);
        this.photoBusy.set(false);
        this.photoPreview.set(null);
      },
      error: (e: unknown) => {
        this.photoBusy.set(false);
        this.photoPreview.set(null);
        this.toast.error(apiErrorMessage(e, 'Could not upload the photo.'));
      },
    });
  }

  sendPhoneOtp(): void {
    this.phoneBusy.set(true);
    this.phoneError.set(null);
    this.driverService.requestPhoneOtp(this.newPhone).subscribe({
      next: () => {
        this.phoneBusy.set(false);
        this.phoneOtpSent.set(true);
      },
      error: (e: unknown) => {
        this.phoneBusy.set(false);
        this.phoneError.set(apiErrorMessage(e));
      },
    });
  }

  verifyPhone(): void {
    this.phoneBusy.set(true);
    this.phoneError.set(null);
    this.driverService.verifyPhone(this.newPhone, this.phoneOtp).subscribe({
      next: (p) => {
        this.apply(p, false);
        this.auth.updateCached({ phoneNumber: p.phoneNumber });
        this.phoneBusy.set(false);
        this.phoneOtpSent.set(false);
        this.changingPhone.set(false);
        this.newPhone = '';
        this.phoneOtp = '';
        this.toast.success('WhatsApp number verified.');
      },
      error: (e: unknown) => {
        this.phoneBusy.set(false);
        this.phoneError.set(apiErrorMessage(e));
      },
    });
  }

  onDoc(event: Event): void {
    const file = this.takeFile(event);
    if (!file) return;
    this.docBusy.set(true);
    this.docError.set(null);
    this.driverService.uploadDocument(file, this.docType, null).subscribe({
      next: () => {
        this.docBusy.set(false);
        this.toast.success('Document uploaded.');
        this.reload();
      },
      error: (e: unknown) => {
        this.docBusy.set(false);
        this.docError.set(apiErrorMessage(e, 'Could not upload the document.'));
      },
    });
  }

  view(d: DriverDocument): void {
    // Open the tab inside the click (popup blockers), then point it at the downloaded file.
    const tab = window.open('', '_blank');
    this.driverService.documentFile(d.driverDocumentId).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        if (tab) tab.location.href = url;
        else window.location.href = url;
      },
      error: (e: unknown) => {
        tab?.close();
        this.toast.error(apiErrorMessage(e, 'Could not open the document.'));
      },
    });
  }

  removeDoc(d: DriverDocument): void {
    if (!confirm(`Remove this ${this.docLabel(d.documentType).toLowerCase()}?`)) return;
    this.driverService.deleteDocument(d.driverDocumentId).subscribe({
      next: () => this.reload(),
      error: (e: unknown) => this.toast.error(apiErrorMessage(e)),
    });
  }

  submit(): void {
    this.submitting.set(true);
    this.submitError.set(null);
    this.driverService.submitProfile().subscribe({
      next: (p) => {
        this.apply(p, false);
        this.submitting.set(false);
        this.toast.success('Sent for approval.');
      },
      error: (e: unknown) => {
        this.submitting.set(false);
        this.submitError.set(apiErrorMessage(e));
      },
    });
  }

  docLabel(t: DriverDocumentType): string {
    return DriverDocumentTypeLabels[t];
  }

  private reload(): void {
    this.driverService.profile().subscribe({ next: (p) => this.apply(p, false), error: () => undefined });
  }

  /** Shows the latest profile. Only a load or save refills the form — a photo, document or phone update
   *  must not wipe what the driver is typing meanwhile. */
  private apply(p: DriverProfile, refillForm = true): void {
    this.profile.set(p);
    this.auth.updateCached({ name: p.name, status: p.status });
    if (!refillForm) return;
    this.form = {
      name: p.name,
      email: p.email ?? '',
      address: p.address ?? '',
      city: p.city ?? '',
      drivingLicenceNumber: p.drivingLicenceNumber ?? '',
      licenceExpiryDate: p.licenceExpiryDate ?? '',
      experienceYears: p.experienceYears,
    };
  }

  private takeFile(event: Event): File | null {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    input.value = '';
    return file;
  }
}
