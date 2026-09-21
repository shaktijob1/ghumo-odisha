import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CustomerAuthResponse } from '../../core/models/auth.model';
import { WhatsappAuthComponent } from '../../shared/components/whatsapp-auth.component';

@Component({
  selector: 'app-customer-auth',
  standalone: true,
  imports: [WhatsappAuthComponent],
  templateUrl: './customer-auth.component.html',
})
export class CustomerAuthComponent {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  onAuthenticated(_response: CustomerAuthResponse): void {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    this.router.navigateByUrl(returnUrl || '/my-bookings');
  }
}
