import { Injectable, signal } from '@angular/core';

/** Opens the Terms & Conditions popup, which the customer layout renders once for every page. */
@Injectable({ providedIn: 'root' })
export class TermsDialogService {
  readonly isOpen = signal(false);

  open(): void {
    this.isOpen.set(true);
  }

  close(): void {
    this.isOpen.set(false);
  }
}
