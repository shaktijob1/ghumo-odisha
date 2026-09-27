import { Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LoginModalService } from '../../core/services/login-modal.service';
import { ToastService } from '../../core/services/toast.service';
import { CustomerAuthResponse } from '../../core/models/auth.model';
import { WhatsappAuthComponent } from './whatsapp-auth.component';

const CLOSE_ANIMATION_MS = 180;
const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), iframe, [tabindex]:not([tabindex="-1"])';

/**
 * The navbar "Sign in" popup: a benefits panel (desktop only) beside the sign-in panel, which is
 * the existing WhatsappAuthComponent in its "popup" layout — same WhatsApp OTP / Google flows and
 * services, nothing duplicated. Rendered only while open (see CustomerLayoutComponent).
 */
@Component({
  selector: 'app-login-modal',
  standalone: true,
  imports: [RouterLink, WhatsappAuthComponent],
  templateUrl: './login-modal.component.html',
})
export class LoginModalComponent implements OnInit, OnDestroy {
  private readonly modal = inject(LoginModalService);
  private readonly toast = inject(ToastService);

  @ViewChild('dialog', { static: true }) dialog!: ElementRef<HTMLElement>;

  readonly closing = signal(false);

  private previousFocus: HTMLElement | null = null;
  private previousOverflow = '';

  ngOnInit(): void {
    this.previousFocus = document.activeElement as HTMLElement | null;
    this.previousOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    // After the first render, put the cursor in the mobile-number field.
    setTimeout(() => (this.dialog.nativeElement.querySelector<HTMLInputElement>('#lm-phone') ?? this.firstFocusable())?.focus(), 50);
  }

  ngOnDestroy(): void {
    document.body.style.overflow = this.previousOverflow;
    this.previousFocus?.focus?.();
  }

  close(): void {
    if (this.closing()) return;
    this.closing.set(true);
    setTimeout(() => this.modal.close(), CLOSE_ANIMATION_MS);
  }

  onAuthenticated(response: CustomerAuthResponse): void {
    // Stay on the current page — the navbar switches to "Hi, name" on its own.
    this.toast.success(response.name ? `Welcome, ${response.name.split(' ')[0]}!` : 'You are signed in.');
    this.close();
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) this.close();
  }

  @HostListener('document:keydown', ['$event'])
  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
      return;
    }
    if (event.key !== 'Tab') return;

    // Keep keyboard focus inside the dialog.
    const items = this.focusables();
    if (items.length === 0) return;
    const first = items[0];
    const last = items[items.length - 1];
    const active = document.activeElement;
    const inside = this.dialog.nativeElement.contains(active);
    if (event.shiftKey && (active === first || !inside)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (active === last || !inside)) {
      event.preventDefault();
      first.focus();
    }
  }

  private focusables(): HTMLElement[] {
    return Array.from(this.dialog.nativeElement.querySelectorAll<HTMLElement>(FOCUSABLE))
      .filter((el) => el.offsetParent !== null || el === document.activeElement);
  }

  private firstFocusable(): HTMLElement | undefined {
    return this.focusables()[0];
  }
}
