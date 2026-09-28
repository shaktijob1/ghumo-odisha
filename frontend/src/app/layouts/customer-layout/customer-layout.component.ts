import { Component, ElementRef, HostListener, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { ContactService } from '../../core/services/contact.service';
import { LoginModalService } from '../../core/services/login-modal.service';
import { ToastHostComponent } from '../../shared/components/toast-host.component';
import { LoginModalComponent } from '../../shared/components/login-modal.component';
import { TermsDialogComponent } from '../../shared/components/terms-dialog.component';
import { TermsDialogService } from '../../core/services/terms-dialog.service';

@Component({
  selector: 'app-customer-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastHostComponent, LoginModalComponent, TermsDialogComponent],
  templateUrl: './customer-layout.component.html',
})
export class CustomerLayoutComponent {
  readonly auth = inject(CustomerAuthService);
  readonly loginModal = inject(LoginModalService);
  readonly termsDialog = inject(TermsDialogService);
  private readonly contactService = inject(ContactService);
  private readonly host = inject(ElementRef<HTMLElement>);
  readonly contact = this.contactService.get();
  readonly whatsAppLink = computed(() =>
    this.contact()?.whatsAppNumber ? this.contactService.buildWhatsAppLink('Hi Ghumo Odisha! I would like to know more about your trips.') : null,
  );
  readonly menuOpen = signal(false);
  readonly accountOpen = signal(false);
  readonly year = new Date().getFullYear();

  readonly firstName = computed(() => this.auth.currentCustomer()?.name?.split(' ')?.[0] || 'there');

  toggleMenu(): void {
    this.menuOpen.update((v) => !v);
  }

  toggleAccount(): void {
    this.accountOpen.update((v) => !v);
  }

  closeAccount(): void {
    this.accountOpen.set(false);
  }

  signOut(): void {
    this.closeAccount();
    this.auth.logout();
  }

  // Close the desktop account menu on any click outside it, or on Escape.
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.accountOpen()) return;
    const acct = (this.host.nativeElement as HTMLElement).querySelector('.acct');
    if (acct && !acct.contains(event.target as Node)) this.closeAccount();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeAccount();
  }
}
