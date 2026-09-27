import { Component, OnInit, inject } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AdminAuthService } from '../../core/services/admin-auth.service';
import { AdminRefundService } from '../../core/services/admin-refund.service';
import { ToastHostComponent } from '../../shared/components/toast-host.component';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastHostComponent],
  templateUrl: './admin-layout.component.html',
})
export class AdminLayoutComponent implements OnInit {
  readonly auth = inject(AdminAuthService);
  readonly refunds = inject(AdminRefundService);
  private readonly router = inject(Router);

  ngOnInit(): void {
    // Keep the "Refunds" badge current: refresh the pending count on every admin page change
    // (a cancellation elsewhere in the admin adds one).
    this.refreshRefundCount();
    this.router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe(() => this.refreshRefundCount());
  }

  signOut(): void {
    this.auth.logout();
  }

  private refreshRefundCount(): void {
    this.refunds.getCounts().subscribe({ error: () => undefined });
  }
}
