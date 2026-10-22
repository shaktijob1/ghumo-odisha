import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { ToastService } from '../../../../core/services/toast.service';
import {
  CrmImportOutcome,
  CrmImportPreview,
  CrmImportResult,
  CrmImportRow,
  CrmStatusLabels,
  LeadSourceLabels,
} from '../../../../core/models/crm.model';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { downloadFile } from '../../../../shared/utils/download-file';
import { CsvReadResult, SAMPLE_CSV, parseCsv, toImportRows } from './crm-csv';

const MAX_ROWS = 2000;

/**
 * Admin → CRM → Import: read a CSV in the browser, let the server check every row (mobile, duplicates,
 * trip, departure, stage, source) and show what will happen, then import. Existing mobiles never
 * become a second customer. Excel files: "Save as → CSV" first (no extra library needed).
 */
@Component({
  selector: 'app-crm-import',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './crm-import.component.html',
  styleUrls: ['../crm-shared.css', './crm-import.component.css'],
})
export class CrmImportComponent {
  private readonly crm = inject(AdminCrmService);
  private readonly toast = inject(ToastService);

  readonly Outcome = CrmImportOutcome;
  readonly statusLabels = CrmStatusLabels;
  readonly sourceLabels = LeadSourceLabels;

  readonly fileName = signal<string | null>(null);
  readonly read = signal<CsvReadResult | null>(null);
  readonly preview = signal<CrmImportPreview | null>(null);
  readonly result = signal<CrmImportResult | null>(null);
  readonly busy = signal<'reading' | 'checking' | 'importing' | 'syncing' | null>(null);
  readonly error = signal<string | null>(null);
  readonly filter = signal<CrmImportOutcome | null>(null);
  addInterestToExisting = true;

  downloadSample(): void {
    downloadFile(new Blob([SAMPLE_CSV], { type: 'text/csv;charset=utf-8' }), 'ghumo-odisha-leads-sample.csv');
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    this.reset();
    if (!/\.csv$/i.test(file.name)) {
      this.error.set('Choose a .csv file. In Excel or Google Sheets use “Save as / Download → CSV”.');
      return;
    }

    this.fileName.set(file.name);
    this.busy.set('reading');
    const reader = new FileReader();
    reader.onload = () => {
      const read = toImportRows(parseCsv(String(reader.result ?? '')));
      this.busy.set(null);
      if (read.rows.length === 0) {
        this.error.set('No rows found. The first line must be the column names (Name, Mobile, …).');
        return;
      }
      if (!read.mapped.some((m) => m.field === 'mobile') || !read.mapped.some((m) => m.field === 'name')) {
        this.error.set('The file needs a Name column and a Mobile column.');
        return;
      }
      if (read.rows.length > MAX_ROWS) {
        this.error.set(`The file has ${read.rows.length} rows — import up to ${MAX_ROWS} at a time.`);
        return;
      }
      this.read.set(read);
      this.check();
    };
    reader.onerror = () => {
      this.busy.set(null);
      this.error.set('Could not read the file.');
    };
    reader.readAsText(file, 'utf-8');
  }

  /** Ask the server what importing would do — nothing is saved. */
  check(): void {
    const rows = this.read()?.rows;
    if (!rows) return;
    this.busy.set('checking');
    this.error.set(null);
    this.crm.importPreview(rows, this.addInterestToExisting).subscribe({
      next: (p) => {
        this.busy.set(null);
        this.preview.set(p);
      },
      error: (err) => {
        this.busy.set(null);
        this.error.set(apiErrorMessage(err, 'Could not check the file.'));
      },
    });
  }

  runImport(): void {
    const rows: CrmImportRow[] | undefined = this.read()?.rows;
    if (!rows) return;
    this.busy.set('importing');
    this.crm.importRows(rows, this.addInterestToExisting).subscribe({
      next: (r) => {
        this.busy.set(null);
        this.result.set(r);
        this.toast.success(`Imported: ${r.customersCreated} new customers, ${r.interestsAdded} trip interests.`);
      },
      error: (err) => {
        this.busy.set(null);
        this.error.set(apiErrorMessage(err, 'The import failed.'));
      },
    });
  }

  syncBookings(): void {
    this.busy.set('syncing');
    this.crm.syncBookings().subscribe({
      next: (r) => {
        this.busy.set(null);
        this.toast.success(r.linked === 0 ? 'Every booking is already in the CRM.' : `${r.linked} booking(s) added to the CRM.`);
      },
      error: (err) => {
        this.busy.set(null);
        this.toast.error(apiErrorMessage(err, 'Could not sync bookings.'));
      },
    });
  }

  reset(): void {
    this.fileName.set(null);
    this.read.set(null);
    this.preview.set(null);
    this.result.set(null);
    this.error.set(null);
    this.filter.set(null);
  }

  visibleRows() {
    const filter = this.filter();
    const rows = this.preview()?.rows ?? [];
    return filter === null ? rows : rows.filter((r) => r.outcome === filter || (filter === CrmImportOutcome.Existing && r.outcome === CrmImportOutcome.SameAsEarlierRow));
  }

  /** How many rows will actually create or change something with the current setting. */
  willChange(): number {
    return (this.preview()?.rows ?? []).filter(
      (r) =>
        r.outcome === CrmImportOutcome.New ||
        (r.outcome === CrmImportOutcome.SameAsEarlierRow && r.tripId !== null) ||
        (r.outcome === CrmImportOutcome.Existing && r.tripId !== null && this.addInterestToExisting),
    ).length;
  }
}
