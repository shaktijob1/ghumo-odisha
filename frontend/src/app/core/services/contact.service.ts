import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';
import { ContactInfo } from '../models/auth.model';

@Injectable({ providedIn: 'root' })
export class ContactService {
  private readonly contact = signal<ContactInfo | null>(null);
  private loaded = false;

  constructor(private readonly http: HttpClient) {}

  get(): typeof this.contact {
    if (!this.loaded) {
      this.loaded = true;
      this.http.get<ApiResponse<ContactInfo>>(`${environment.apiUrl}/contact`).subscribe((r) => {
        if (r.data) this.contact.set(r.data);
      });
    }
    return this.contact;
  }

  buildWhatsAppLink(message: string): string {
    const number = this.contact()?.whatsAppNumber ?? '';
    return `https://wa.me/${number}?text=${encodeURIComponent(message)}`;
  }
}
