import { Component, ElementRef, HostListener, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { ContactService } from '../../core/services/contact.service';
import { LoginModalService } from '../../core/services/login-modal.service';
import { ToastHostComponent } from '../../shared/components/toast-host.component';
import { LoginModalComponent } from '../../shared/components/login-modal.component';
import { TermsDialogComponent } from '../../shared/components/terms-dialog.component';
import { TermsDialogService } from '../../core/services/terms-dialog.service';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';
import { OdishaMarkComponent } from '../../shared/components/odisha-mark.component';

@Component({
  selector: 'app-customer-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ToastHostComponent, LoginModalComponent, TermsDialogComponent, ImageUrlPipe, OdishaMarkComponent],
  templateUrl: './customer-layout.component.html',
  styles: `
    .logo-mark { display: inline-flex; align-items: center; gap: 8px; }
    .ft { border-top: 1px solid var(--line); background: #fff; margin-top: 26px; }
    .ft-in { display: grid; grid-template-columns: 1fr minmax(0, 320px); gap: 32px 48px; max-width: 1240px; margin: 0 auto; padding: 32px 28px 8px; }
    .ft-col { display: flex; flex-direction: column; gap: 10px; min-width: 0; }
    .ft-brand p { margin: 0; max-width: 40ch; font-size: 13px; line-height: 1.65; color: var(--muted); }
    .ft-lbl { font-size: 12px; font-weight: 600; color: var(--muted); letter-spacing: .04em; text-transform: uppercase; }
    .ft-brand .ft-lbl { margin-top: 6px; }
    .ft-icons { display: flex; gap: 12px; }
    .ft-office { display: block; max-width: 320px; border: 1px solid var(--line); border-radius: var(--radius-card); overflow: hidden; background: #fff; color: var(--ink); transition: box-shadow .2s ease, border-color .2s ease; }
    .ft-office:hover { border-color: #BFDDD4; box-shadow: 0 10px 24px rgba(15,111,92,.12); }
    .ft-office img { display: block; width: 100%; height: 150px; object-fit: cover; background: var(--canvas); }
    .ft-office address { font-style: normal; padding: 12px 14px 4px; font-size: 13px; line-height: 1.55; color: var(--muted); }
    .ft-office address b { display: block; font-size: 14px; color: var(--ink); }
    .ft-dir { display: inline-flex; align-items: center; gap: 6px; padding: 4px 14px 14px; font-size: 13px; font-weight: 600; color: var(--accent); }
    .ft .footbar { margin-top: 22px; }
    @media (max-width: 640px) {
      .ft-in { grid-template-columns: 1fr; gap: 26px; padding: 26px 16px 4px; }
      .ft-office { max-width: none; }
      .ft-office img { height: 170px; }
    }
  `,})
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
  readonly accountOpen = signal(false);
  readonly year = new Date().getFullYear();

  readonly firstName = computed(() => this.auth.currentCustomer()?.name?.split(' ')?.[0] || 'there');

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

  // Close the account menu on any click (or tap) outside it, or on Escape.
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
