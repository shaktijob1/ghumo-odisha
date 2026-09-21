import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { ContactService } from '../../core/services/contact.service';
import { ToastHostComponent } from '../../shared/components/toast-host.component';

@Component({
  selector: 'app-customer-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastHostComponent],
  templateUrl: './customer-layout.component.html',
})
export class CustomerLayoutComponent {
  readonly auth = inject(CustomerAuthService);
  private readonly contactService = inject(ContactService);
  readonly contact = this.contactService.get();
  readonly menuOpen = signal(false);
  readonly year = new Date().getFullYear();

  toggleMenu(): void {
    this.menuOpen.update((v) => !v);
  }
}
