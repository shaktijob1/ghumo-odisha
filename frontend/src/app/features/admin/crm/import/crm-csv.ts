import { CrmImportRow } from '../../../../core/models/crm.model';

/**
 * Minimal CSV reader (RFC 4180): commas, quoted fields, "" inside quotes, line breaks inside quotes,
 * \r\n or \n line endings, and a UTF-8 BOM from Excel. Returns rows of cells.
 */
export function parseCsv(text: string): string[][] {
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = '';
  let quoted = false;
  const src = text.replace(/^﻿/, '');

  for (let i = 0; i < src.length; i++) {
    const ch = src[i];
    if (quoted) {
      if (ch === '"') {
        if (src[i + 1] === '"') {
          cell += '"';
          i++;
        } else {
          quoted = false;
        }
      } else {
        cell += ch;
      }
    } else if (ch === '"') {
      quoted = true;
    } else if (ch === ',') {
      row.push(cell);
      cell = '';
    } else if (ch === '\n' || ch === '\r') {
      if (ch === '\r' && src[i + 1] === '\n') i++;
      row.push(cell);
      rows.push(row);
      row = [];
      cell = '';
    } else {
      cell += ch;
    }
  }
  if (cell !== '' || row.length > 0) {
    row.push(cell);
    rows.push(row);
  }
  return rows.filter((r) => r.some((c) => c.trim() !== ''));
}

/** Header names (lower-case, letters only) each column is recognised by. */
const COLUMNS: Record<keyof CrmImportRow, string[]> = {
  name: ['name', 'fullname', 'customer', 'customername'],
  mobile: ['mobile', 'phone', 'mobilenumber', 'phonenumber', 'whatsapp', 'contact'],
  email: ['email', 'emailaddress'],
  city: ['city', 'location', 'place'],
  trip: ['trip', 'tripname', 'package'],
  departure: ['departure', 'departuredate', 'date', 'traveldate'],
  status: ['status', 'stage', 'customerstatus'],
  source: ['source', 'leadsource', 'channel'],
  notes: ['notes', 'note', 'remarks', 'comments'],
  travellers: ['travellers', 'travelers', 'pax', 'people', 'seats'],
};

export interface CsvReadResult {
  rows: CrmImportRow[];
  /** Which file column fed which field, for the admin to check. */
  mapped: { field: keyof CrmImportRow; header: string }[];
  ignored: string[];
}

/** Turns parsed CSV (first row = headers) into import rows; unknown columns are ignored. */
export function toImportRows(table: string[][]): CsvReadResult {
  if (table.length === 0) return { rows: [], mapped: [], ignored: [] };
  const headers = table[0].map((h) => h.trim());
  const key = (h: string) => h.toLowerCase().replace(/[^a-z]/g, '');
  const index: Partial<Record<keyof CrmImportRow, number>> = {};
  const mapped: CsvReadResult['mapped'] = [];
  const ignored: string[] = [];

  headers.forEach((header, i) => {
    const field = (Object.keys(COLUMNS) as (keyof CrmImportRow)[]).find((f) => COLUMNS[f].includes(key(header)) && index[f] === undefined);
    if (field) {
      index[field] = i;
      mapped.push({ field, header });
    } else if (header) {
      ignored.push(header);
    }
  });

  const cell = (r: string[], field: keyof CrmImportRow): string | null => {
    const i = index[field];
    const value = i === undefined ? '' : (r[i] ?? '').trim();
    return value === '' ? null : value;
  };

  const rows = table.slice(1).map((r) => {
    const travellers = Number(cell(r, 'travellers'));
    return {
      name: cell(r, 'name'),
      mobile: cell(r, 'mobile'),
      email: cell(r, 'email'),
      city: cell(r, 'city'),
      trip: cell(r, 'trip'),
      departure: cell(r, 'departure'),
      status: cell(r, 'status'),
      source: cell(r, 'source'),
      notes: cell(r, 'notes'),
      travellers: Number.isFinite(travellers) && travellers > 0 ? Math.round(travellers) : null,
    };
  });
  return { rows, mapped, ignored };
}

/** A small example file showing every column the import understands. */
export const SAMPLE_CSV = [
  'Name,Mobile,Email,City,Trip,Departure,Status,Source,Notes,Travellers',
  'Rahul Sahu,9876543210,rahul@example.com,Bhubaneswar,Koraput Escape,2026-11-14,Hot lead,Instagram,"Wants twin sharing, pickup Patia",2',
  'Priya Das,+91 98765 43211,,Cuttack,Puri Konark Escape,,Interested,Facebook,Asked for itinerary,4',
  'Amit Mohanty,09876543212,,,,,New lead,Referral,Referred by Rahul,',
].join('\r\n');
