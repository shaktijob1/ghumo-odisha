import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiResponse } from '../models/api-response.model';

export const DEFAULT_TITLE = 'Ghumo Odisha | Odisha Group Trips & Travel Packages';

/** What the API decides for one page (SeoPageRenderer.GetMetaAsync) — the same tags the first HTML response carried. */
interface SeoMeta {
  title: string;
  description: string;
  canonicalUrl: string | null;
  imageUrl: string | null;
  ogType: string;
  noIndex: boolean;
  statusCode: number;
  jsonLd: string[];
}

/**
 * Keeps the page's title, description, canonical link, robots rule, share-preview tags and
 * schema.org data right as the visitor moves around the app.
 *
 * The API is the single source of truth for public pages: it writes these tags into the first
 * HTML response (SeoPageRenderer) and serves the same values from /api/seo/meta, which this
 * service asks for after every client-side navigation. A new trip or destination therefore needs
 * no change here. Private pages (account, admin, driver) are marked noindex without a request.
 */
@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly http = inject(HttpClient);
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);

  // The server already wrote the right tags for the page the visitor landed on.
  private firstPath: string | null = this.currentPath();
  private requestId = 0;

  /** Public page: fetch its tags from the API (skipped for the page the server just rendered). */
  updatePublicPage(path: string, fallbackTitle: string | null): void {
    if (path === this.firstPath) {
      this.firstPath = null;
      return;
    }
    this.firstPath = null;

    const id = ++this.requestId;
    if (fallbackTitle) this.title.setTitle(fallbackTitle);
    this.http
      .get<ApiResponse<SeoMeta>>(`${environment.apiUrl}/seo/meta`, { params: { path } })
      .pipe(map((r) => r.data!))
      .subscribe({
        next: (m) => {
          if (id === this.requestId) this.apply(m);
        },
        // Tags are best-effort: a failed lookup leaves the previous values rather than breaking the page.
        error: () => undefined,
      });
  }

  /** Private or utility page: its own title, never indexed, no canonical or structured data. */
  updatePrivatePage(title: string | null): void {
    this.firstPath = null;
    this.requestId++;
    this.title.setTitle(title ?? DEFAULT_TITLE);
    this.meta.updateTag({ name: 'robots', content: 'noindex, nofollow' });
    this.setCanonical(null);
    this.setJsonLd([]);
  }

  private apply(m: SeoMeta): void {
    this.title.setTitle(m.title);
    const tags: [string, string, string | null][] = [
      ['name', 'description', m.description],
      ['name', 'robots', m.noIndex ? 'noindex, nofollow' : 'index, follow, max-image-preview:large'],
      ['property', 'og:type', m.ogType],
      ['property', 'og:title', m.title],
      ['property', 'og:description', m.description],
      ['property', 'og:url', m.canonicalUrl],
      ['property', 'og:image', m.imageUrl],
      ['property', 'og:image:alt', m.imageUrl && m.title],
      ['name', 'twitter:card', m.imageUrl ? 'summary_large_image' : 'summary'],
      ['name', 'twitter:title', m.title],
      ['name', 'twitter:description', m.description],
      ['name', 'twitter:image', m.imageUrl],
    ];
    for (const [attr, key, content] of tags) {
      if (content) this.meta.updateTag({ [attr]: key, content });
      else this.meta.removeTag(`${attr}="${key}"`);
    }
    this.setCanonical(m.canonicalUrl);
    this.setJsonLd(m.jsonLd);
  }

  private setCanonical(url: string | null): void {
    let link = this.document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!url) {
      link?.remove();
      return;
    }
    if (!link) {
      link = this.document.createElement('link');
      link.rel = 'canonical';
      this.document.head.appendChild(link);
    }
    link.href = url;
  }

  /** Replaces the page's schema.org blocks (the server marks its own with data-seo too). */
  private setJsonLd(blocks: string[]): void {
    this.document.head.querySelectorAll('script[type="application/ld+json"][data-seo]').forEach((s) => s.remove());
    for (const json of blocks) {
      const script = this.document.createElement('script');
      script.type = 'application/ld+json';
      script.setAttribute('data-seo', '');
      script.textContent = json;
      this.document.head.appendChild(script);
    }
  }

  private currentPath(): string {
    const loc = this.document.location;
    return loc ? loc.pathname || '/' : '/';
  }
}
