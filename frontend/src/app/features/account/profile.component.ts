import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CustomerProfileService } from '../../core/services/customer-profile.service';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { CustomerProfile } from '../../core/models/auth.model';
import { ToastService } from '../../core/services/toast.service';
import { StatePanelComponent } from '../../shared/components/state-panel.component';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, FormsModule, StatePanelComponent],
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

  editName = '';
  editEmail = '';

  ngOnInit(): void {
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

  signOut(): void {
    this.auth.logout();
  }
}
