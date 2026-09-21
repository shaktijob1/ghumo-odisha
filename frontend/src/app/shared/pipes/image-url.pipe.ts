import { Pipe, PipeTransform } from '@angular/core';
import { environment } from '../../../environments/environment';

/**
 * Uploaded file URLs come back from the API as origin-relative paths (e.g. "/uploads/trips/x.jpg").
 * In dev, the Angular app and the API run on different ports, so a bare relative path resolves
 * against the wrong origin. This prefixes it with the API's origin; in prod (same-origin, reverse
 * proxied) apiOrigin is empty and this is a no-op.
 */
@Pipe({ name: 'imageUrl', standalone: true })
export class ImageUrlPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) return '';
    if (/^https?:\/\//i.test(value)) return value;
    return `${environment.apiOrigin}${value}`;
  }
}
