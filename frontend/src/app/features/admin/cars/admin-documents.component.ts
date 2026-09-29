import { Component, inject, input, signal } from '@angular/core';
import { DriverDocumentTypeLabels } from '../../../core/models/car.model';
import { DriverDocument } from '../../../core/models/driver.model';
import { AdminCarService } from '../../../core/services/admin-car.service';
import { istDateTime } from '../../../shared/utils/car-format';

/**
 * A driver's private documents (licence, RC, insurance…). They're never on a public URL: each one is
 * fetched with the admin token and opened from a temporary blob link.
 */
@Component({
  selector: 'app-admin-documents',
  standalone: true,
  template: `
    @if (documents().length === 0) {
      <p class="note">{{ emptyText() }}</p>
    } @else {
      @for (d of documents(); track d.driverDocumentId) {
        <div class="row sp adoc">
          <div>
            <b>{{ labels[d.documentType] }}</b>@if (carId() !== null && d.carId === null) { <span class="note"> · driver</span> }
            <div class="note">Uploaded {{ when(d.createdAt) }}{{ d.contentType === 'application/pdf' ? ' · PDF' : '' }}</div>
          </div>
          <button type="button" class="btn sm ghost" [disabled]="opening() === d.driverDocumentId" (click)="open(d)">
            @if (opening() === d.driverDocumentId) { <span class="spin"></span> } @else { View }
          </button>
        </div>
      }
      @if (error()) { <div class="errorbox">{{ error() }}</div> }
    }
  `,
})
export class AdminDocumentsComponent {
  private readonly cars = inject(AdminCarService);

  readonly documents = input.required<DriverDocument[]>();
  readonly emptyText = input('No documents uploaded.');
  /** On a car page: marks the driver's own documents (licence) apart from the car's. */
  readonly carId = input<number | null>(null);
  readonly labels = DriverDocumentTypeLabels;
  readonly when = istDateTime;
  readonly opening = signal<number | null>(null);
  readonly error = signal<string | null>(null);

  open(doc: DriverDocument): void {
    // Open the tab now (inside the click) so pop-up blockers allow it, then point it at the file.
    const tab = window.open('', '_blank');
    this.opening.set(doc.driverDocumentId);
    this.error.set(null);
    this.cars.documentFile(doc.driverDocumentId).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        if (tab) tab.location.href = url;
        else window.location.assign(url);
        setTimeout(() => URL.revokeObjectURL(url), 60_000);
        this.opening.set(null);
      },
      error: () => {
        tab?.close();
        this.opening.set(null);
        this.error.set('Could not open this document.');
      },
    });
  }
}
