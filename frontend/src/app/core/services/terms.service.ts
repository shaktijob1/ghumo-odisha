import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';

export interface TermsAndConditions {
  version: string;
  text: string;
}

export interface TermsSection {
  heading: string | null;
  body: string;
}

/**
 * Splits the plain-text terms into sections: paragraphs are separated by blank lines, a paragraph
 * whose first line starts "N." is a numbered heading + body, and hard line wraps are joined back
 * into flowing text. The first paragraph is the document title and is dropped — callers show their own.
 */
export function parseTerms(text: string): TermsSection[] {
  return text
    .split(/\n\s*\n/)
    .map((p) => p.split('\n').map((l) => l.trim()).filter(Boolean))
    .filter((lines) => lines.length > 0)
    .slice(1)
    .map((lines) => {
      const isHeading = /^\d+\.\s/.test(lines[0]);
      return {
        heading: isHeading ? lines[0] : null,
        body: (isHeading ? lines.slice(1) : lines).join(' '),
      };
    });
}

@Injectable({ providedIn: 'root' })
export class TermsService {
  constructor(private readonly http: HttpClient) {}

  get(): Observable<TermsAndConditions> {
    return this.http.get<ApiResponse<TermsAndConditions>>(`${environment.apiUrl}/terms`).pipe(map((r) => r.data!));
  }
}
