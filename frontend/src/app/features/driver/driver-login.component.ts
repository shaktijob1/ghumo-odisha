import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { DriverAuthService } from '../../core/services/driver-auth.service';
import { FeatureService } from '../../core/services/feature.service';
import { GoogleSignInButtonComponent } from '../../shared/components/google-sign-in-button.component';
import { apiErrorMessage } from '../../shared/utils/api-error';

/** /driver/login — drivers sign in (or sign up) with WhatsApp OTP or Google. */
@Component({
  selector: 'app-driver-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, GoogleSignInButtonComponent],
  template: `
    <div class="dv-auth">
      <a routerLink="/" class="dv-logo">Ghumo <em>Odisha</em></a>
      <div class="card pad dv-authcard">
        <span class="cz-cat">For drivers</span>
        <h1>Drive with Ghumo Odisha</h1>
        <p class="mut">Sign in or create your driver account. Add your car, set your prices, and get bookings once we approve you.</p>

        @if (step() === 'phone') {
          <form (ngSubmit)="sendOtp()">
            <div class="fld">
              <label class="lbl" for="dv-name">Your name <span class="mut">(new drivers)</span></label>
              <input id="dv-name" class="inp" name="name" autocomplete="name" [(ngModel)]="name" maxlength="150" />
            </div>
            <div class="fld">
              <label class="lbl" for="dv-phone">WhatsApp number</label>
              <div class="inp numprefix">
                <span class="numprefix-code">+91</span>
                <input id="dv-phone" name="phone" type="tel" inputmode="numeric" autocomplete="tel-national" maxlength="10" placeholder="10-digit number" [(ngModel)]="phone" />
              </div>
            </div>
            @if (error()) { <div class="errorbox">{{ error() }}</div> }
            <button type="submit" class="btn block" [disabled]="busy() || phone.length !== 10">
              @if (busy()) { <span class="spin"></span> } @else { Send OTP on WhatsApp }
            </button>
          </form>
          <div class="dv-or"><span>or</span></div>
          <div class="dv-google"><app-google-sign-in-button text="continue_with" (credential)="google($event)"></app-google-sign-in-button></div>
        } @else {
          <form (ngSubmit)="verify()">
            <p class="note" style="margin-bottom:12px">We sent a code to <b>+91 {{ phone }}</b> on WhatsApp.
              <button type="button" class="linkbtn" (click)="step.set('phone'); error.set(null)">Change</button></p>
            <div class="fld">
              <label class="lbl" for="dv-otp">OTP</label>
              <input id="dv-otp" class="inp dv-otp" name="otp" inputmode="numeric" autocomplete="one-time-code" [maxlength]="otpLength()" [(ngModel)]="otp" />
            </div>
            @if (error()) { <div class="errorbox">{{ error() }}</div> }
            <button type="submit" class="btn block" [disabled]="busy() || otp.length !== otpLength()">
              @if (busy()) { <span class="spin"></span> } @else { Verify & continue }
            </button>
            <button type="button" class="linkbtn" style="display:block;margin:12px auto 0" [disabled]="busy()" (click)="sendOtp()">Resend OTP</button>
          </form>
        }
      </div>
      @if (!features.hideCarsAndHolidays()) {
        <p class="note" style="text-align:center">Looking to book a car? <a routerLink="/cars" style="color:var(--accent);font-weight:600">Go to Cars</a></p>
      }
    </div>
  `,
})
export class DriverLoginComponent implements OnInit {
  private readonly auth = inject(DriverAuthService);
  /** The customer Cars link hides while appsettings Features:HideCarsAndHolidays is on. */
  readonly features = inject(FeatureService);
  private readonly router = inject(Router);

  readonly step = signal<'phone' | 'otp'>('phone');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly otpLength = signal(6);
  name = '';
  phone = '';
  otp = '';

  ngOnInit(): void {
    if (this.auth.isAuthenticated()) this.router.navigate(['/driver']);
    this.features.load().subscribe();
  }

  sendOtp(): void {
    this.phone = this.phone.replace(/\D/g, '');
    this.busy.set(true);
    this.error.set(null);
    this.auth.requestOtp(this.name.trim() || null, this.phone).subscribe({
      next: (r) => {
        this.otpLength.set(r.otpLength);
        this.otp = '';
        this.step.set('otp');
        this.busy.set(false);
      },
      error: (e: unknown) => this.fail(e),
    });
  }

  verify(): void {
    this.busy.set(true);
    this.error.set(null);
    this.auth.verifyOtp(this.phone, this.otp).subscribe({ next: () => this.router.navigate(['/driver']), error: (e: unknown) => this.fail(e) });
  }

  google(credential: string): void {
    this.busy.set(true);
    this.error.set(null);
    this.auth.googleSignIn(credential).subscribe({ next: () => this.router.navigate(['/driver']), error: (e: unknown) => this.fail(e) });
  }

  private fail(e: unknown): void {
    this.busy.set(false);
    this.error.set(apiErrorMessage(e));
  }
}
