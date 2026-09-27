import { Injectable, signal } from '@angular/core';

/**
 * Open/closed state of the navbar sign-in popup. UI only — signing in itself still goes through
 * CustomerAuthService via the existing WhatsappAuthComponent.
 */
@Injectable({ providedIn: 'root' })
export class LoginModalService {
  readonly isOpen = signal(false);

  open(): void {
    this.isOpen.set(true);
  }

  close(): void {
    this.isOpen.set(false);
  }
}
