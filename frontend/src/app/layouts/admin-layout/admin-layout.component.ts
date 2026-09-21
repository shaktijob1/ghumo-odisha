import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { ToastHostComponent } from '../../shared/components/toast-host.component';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastHostComponent],
  templateUrl: './admin-layout.component.html',
})
export class AdminLayoutComponent {
  readonly auth = inject(AdminAuthService);

  signOut(): void {
    this.auth.logout();
  }
}
