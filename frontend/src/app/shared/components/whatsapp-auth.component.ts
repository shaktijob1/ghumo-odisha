import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  Component,
  ElementRef,
  EventEmitter,
  Input,
  OnDestroy,
  Output,
  QueryList,
  ViewChildren,
  inject,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CustomerAuthService } from '../../core/services/customer-auth.service';
import { ApiResponse } from '../../core/models/api-response.model';
import { CustomerAuthResponse } from '../../core/models/auth.model';

type Step = 'details' | 'otp';

@Component({
  selector: 'app-whatsapp-auth',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './whatsapp-auth.component.html',
})
export class WhatsappAuthComponent implements OnDestroy {
  @Input() mode: 'book' | 'login' = 'book';
  @Input() title = 'Book this trip';
  @Output() authenticated = new EventEmitter<CustomerAuthResponse>();
  @Output() cancelled = new EventEmitter<void>();

  @ViewChildren('otpBox') otpBoxes!: QueryList<ElementRef<HTMLInputElement>>;

  private readonly auth = inject(CustomerAuthService);

  readonly step = signal<Step>('details');
  readonly otpLength = signal(6);
  readonly otpDigits = signal<string[]>(Array(6).fill(''));
  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly resendCountdown = signal(0);

  whatsAppNumber = '';

  private countdownTimer?: ReturnType<typeof setInterval>;

  ngOnDestroy(): void {
    if (this.countdownTimer) clearInterval(this.countdownTimer);
  }

  sendOtp(): void {
    this.errorMessage.set(null);

    if (!/^[6-9]\d{9}$/.test(this.whatsAppNumber)) {
      this.errorMessage.set('Enter a valid 10-digit WhatsApp number.');
      return;
    }

    this.submitting.set(true);
    // No name collected here — new customers sign in by phone number alone. The app asks for a
    // name later, before their first booking, if one still isn't on file.
    this.auth.requestOtp(null, this.whatsAppNumber).subscribe({
      next: (r: ApiResponse<{ otpLength: number }>) => {
        this.submitting.set(false);
        const length = r.data?.otpLength ?? 6;
        this.otpLength.set(length);
        this.otpDigits.set(Array(length).fill(''));
        this.step.set('otp');
        this.startCountdown();
        setTimeout(() => this.otpBoxes?.first?.nativeElement.focus(), 0);
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        const body = err.error as ApiResponse<unknown> | undefined;
        this.errorMessage.set(body?.message ?? body?.errors?.join(' ') ?? 'Unable to send OTP right now. Please try again.');
      },
    });
  }

  resendOtp(): void {
    if (this.resendCountdown() > 0 || this.submitting()) return;
    this.sendOtp();
  }

  changeNumber(): void {
    this.step.set('details');
    this.errorMessage.set(null);
    this.otpDigits.set(Array(this.otpLength()).fill(''));
    if (this.countdownTimer) clearInterval(this.countdownTimer);
    this.resendCountdown.set(0);
  }

  cancel(): void {
    this.cancelled.emit();
  }

  onDigitInput(index: number, event: Event): void {
    const input = event.target as HTMLInputElement;
    const digit = input.value.replace(/\D/g, '').slice(-1);

    const digits = [...this.otpDigits()];
    digits[index] = digit;
    this.otpDigits.set(digits);
    input.value = digit;

    if (digit && index < this.otpLength() - 1) {
      this.otpBoxes.get(index + 1)?.nativeElement.focus();
    }

    if (digits.every((d) => d.length === 1)) {
      this.verify();
    }
  }

  onDigitKeydown(index: number, event: KeyboardEvent): void {
    if (event.key === 'Backspace' && !this.otpDigits()[index] && index > 0) {
      this.otpBoxes.get(index - 1)?.nativeElement.focus();
    }
  }

  onDigitPaste(event: ClipboardEvent): void {
    const pasted = event.clipboardData?.getData('text').replace(/\D/g, '') ?? '';
    if (!pasted) return;
    event.preventDefault();

    const digits = Array(this.otpLength()).fill('');
    for (let i = 0; i < Math.min(pasted.length, this.otpLength()); i++) {
      digits[i] = pasted[i];
    }
    this.otpDigits.set(digits);

    this.otpBoxes.forEach((box, i) => (box.nativeElement.value = digits[i] ?? ''));

    const lastFilled = Math.min(pasted.length, this.otpLength()) - 1;
    if (lastFilled >= 0) {
      this.otpBoxes.get(Math.min(lastFilled + 1, this.otpLength() - 1))?.nativeElement.focus();
    }

    if (digits.every((d) => d.length === 1)) {
      this.verify();
    }
  }

  verify(): void {
    const otp = this.otpDigits().join('');
    if (otp.length !== this.otpLength() || this.submitting()) return;

    this.errorMessage.set(null);
    this.submitting.set(true);

    this.auth.verifyOtp(this.whatsAppNumber, otp).subscribe({
      next: (r) => {
        this.submitting.set(false);
        if (r.data) {
          this.authenticated.emit(r.data);
        }
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        const body = err.error as ApiResponse<unknown> | undefined;
        this.errorMessage.set(body?.message ?? 'Invalid OTP.');
        this.otpDigits.set(Array(this.otpLength()).fill(''));
        setTimeout(() => this.otpBoxes?.first?.nativeElement.focus(), 0);
      },
    });
  }

  private startCountdown(): void {
    if (this.countdownTimer) clearInterval(this.countdownTimer);
    this.resendCountdown.set(30);
    this.countdownTimer = setInterval(() => {
      const next = this.resendCountdown() - 1;
      this.resendCountdown.set(Math.max(0, next));
      if (next <= 0 && this.countdownTimer) {
        clearInterval(this.countdownTimer);
      }
    }, 1000);
  }
}
