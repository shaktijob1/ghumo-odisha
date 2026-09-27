import { AfterViewInit, Component, ElementRef, EventEmitter, Input, NgZone, Output, ViewChild, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';

// Minimal typing for the bits of Google Identity Services (accounts.google.com/gsi/client) we use.
interface GoogleIdApi {
  initialize(config: { client_id: string; callback: (r: { credential: string }) => void; ux_mode?: 'popup' }): void;
  renderButton(parent: HTMLElement, options: Record<string, unknown>): void;
}
declare global {
  interface Window {
    google?: { accounts: { id: GoogleIdApi } };
  }
}

const GSI_SRC = 'https://accounts.google.com/gsi/client';
let gsiLoading: Promise<void> | null = null;

/** Loads the Google Identity Services script once per page, on first use. */
function loadGsi(): Promise<void> {
  if (window.google?.accounts?.id) return Promise.resolve();
  if (!gsiLoading) {
    gsiLoading = new Promise<void>((resolve, reject) => {
      const script = document.createElement('script');
      script.src = GSI_SRC;
      script.async = true;
      script.defer = true;
      script.onload = () => resolve();
      script.onerror = () => {
        gsiLoading = null;
        reject(new Error('Google sign-in could not load.'));
      };
      document.head.appendChild(script);
    });
  }
  return gsiLoading;
}

/**
 * Google's own "Sign in with Google" button. Emits the ID token (credential) Google returns;
 * the parent sends it to the API, which verifies it — the browser never decides who someone is.
 */
@Component({
  selector: 'app-google-sign-in-button',
  standalone: true,
  template: `
    <div class="gbtn" #host></div>
    @if (loadFailed()) {
      <p class="note" style="text-align:center">Google sign-in is unavailable right now.</p>
    }
  `,
  styles: [`.gbtn { display: flex; justify-content: center; min-height: 44px; }`],
})
export class GoogleSignInButtonComponent implements AfterViewInit {
  /** Google's button label: "signin_with" | "signup_with" | "continue_with". */
  @Input() text: 'signin_with' | 'signup_with' | 'continue_with' = 'continue_with';
  @Output() credential = new EventEmitter<string>();

  @ViewChild('host', { static: true }) host!: ElementRef<HTMLDivElement>;

  private readonly zone = inject(NgZone);
  readonly loadFailed = signal(false);

  ngAfterViewInit(): void {
    loadGsi()
      .then(() => {
        const id = window.google!.accounts.id;
        id.initialize({
          client_id: environment.googleClientId,
          ux_mode: 'popup',
          // GIS calls back outside Angular's zone — re-enter it so the UI updates.
          callback: (r) => this.zone.run(() => this.credential.emit(r.credential)),
        });
        const width = Math.min(this.host.nativeElement.clientWidth || 320, 400);
        id.renderButton(this.host.nativeElement, {
          type: 'standard',
          theme: 'outline',
          size: 'large',
          shape: 'pill',
          text: this.text,
          logo_alignment: 'center',
          width,
        });
      })
      .catch(() => this.loadFailed.set(true));
  }
}
