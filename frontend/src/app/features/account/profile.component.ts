import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { CustomerProfileService } from '../../core/services/customer-profile.service';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { ApiResponse } from '../../core/models/api-response.model';
import { CustomerProfile } from '../../core/models/auth.model';
import { ToastService } from '../../core/services/toast.service';
import { StatePanelComponent } from '../../shared/components/state-panel.component';
import { GoogleSignInButtonComponent } from '../../shared/components/google-sign-in-button.component';

type LoadState = 'loading' | 'ready' | 'error';
/** Which sign-in method editor is open (only one at a time). */
type Editor = 'none' | 'phone' | 'google';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, StatePanelComponent, GoogleSignInButtonComponent],
  templateUrl: './profile.component.html',
})
export class ProfileComponent implements OnInit {
  private readonly profileService = inject(CustomerProfileService);
  private readonly auth = inject(CustomerAuthService);
  private readonly toast = inject(ToastService);

  readonly state = signal<LoadState>('loading');
  readonly profile = signal<CustomerProfile | null>(null);
  readonly editing = signal(false);
  readonly savingProfile = signal(false);

  readonly editor = signal<Editor>('none');
  readonly busy = signal(false);
  readonly editorError = signal<string | null>(null);
  /** Adding a phone: false = enter the number, true = enter the code we sent. */
  readonly phoneOtpSent = signal(false);

  editName = '';
  editEmail = '';

  newPhone = '';
  phoneOtp = '';

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.profileService.getProfile().subscribe({
      next: (p) => {
        this.profile.set(p);
        this.editName = p.name;
        this.editEmail = p.email || '';
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  startEdit(): void {
    const p = this.profile();
    if (!p) return;
    this.editName = p.name;
    this.editEmail = p.email || '';
    this.editing.set(true);
  }

  saveProfile(): void {
    this.savingProfile.set(true);
    this.profileService.updateProfile({ name: this.editName, email: this.editEmail || null }).subscribe({
      next: () => {
        this.savingProfile.set(false);
        this.editing.set(false);
        this.toast.success('Profile updated.');
        const p = this.profile();
        if (p) this.profile.set({ ...p, name: this.editName, email: this.editEmail || null });
      },
      error: () => this.savingProfile.set(false),
    });
  }

  // ---------- Sign-in methods ----------

  open(editor: Editor): void {
    this.editor.set(editor);
    this.editorError.set(null);
    this.phoneOtpSent.set(false);
    this.newPhone = '';
    this.phoneOtp = '';
  }

  close(): void {
    this.open('none');
  }

  sendPhoneOtp(): void {
    if (!/^[6-9]\d{9}$/.test(this.newPhone)) {
      this.editorError.set('Enter a valid 10-digit WhatsApp number.');
      return;
    }
    this.run(this.profileService.requestAddPhoneOtp(this.newPhone), () => this.phoneOtpSent.set(true));
  }

  verifyPhone(): void {
    if (!/^\d{4,8}$/.test(this.phoneOtp)) {
      this.editorError.set('Enter the code we sent on WhatsApp.');
      return;
    }
    this.run(this.profileService.verifyAddPhone(this.newPhone, this.phoneOtp), () => this.done('WhatsApp number added.'));
  }

  linkGoogle(credential: string): void {
    this.run(this.profileService.linkGoogle(credential), () => this.done('Google account linked.'));
  }

  signOut(): void {
    this.auth.logout();
  }

  private run(request: Observable<unknown>, onSuccess: () => void): void {
    this.editorError.set(null);
    this.busy.set(true);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        onSuccess();
      },
      error: (err: HttpErrorResponse) => {
        this.busy.set(false);
        const body = err.error as ApiResponse<unknown> | undefined;
        this.editorError.set(body?.message ?? body?.errors?.join(' ') ?? 'Something went wrong. Please try again.');
      },
    });
  }

  /** Closes the editor, reloads the profile, and refreshes the cached session (navbar, booking prefill). */
  private done(message: string): void {
    this.toast.success(message);
    this.close();
    this.load();
    this.auth.restoreSession().subscribe();
  }
}
