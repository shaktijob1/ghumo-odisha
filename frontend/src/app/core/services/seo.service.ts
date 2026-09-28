import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { environment } from '../../../environments/environment';

export const DEFAULT_TITLE = 'Ghumo Odisha | Odisha Trips, Tours & Travel Packages';
export const DEFAULT_DESCRIPTION =
  'Book Odisha group trips and tour packages with Ghumo Odisha: Puri, Konark, Chilika, Koraput and more. AC stay and travel, a trip coordinator, fixed departures.';

export interface SeoPage {
  title: string;
  description?: string;
  /** Site path of this page's official address, e.g. "/trips/1-puri-konark". */
  path?: string;
  image?: string | null;
}

/**
 * Keeps the page title, description, canonical link and share-preview tags right as the visitor
 * moves around the app. The API writes the same tags into the first HTML response (SeoPageRenderer)
 * for crawlers that don't run JavaScript; this keeps them correct after client-side navigation.
 */
@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);

  setPage(page: SeoPage): void {
    const description = page.description ?? DEFAULT_DESCRIPTION;
    this.title.setTitle(page.title);
    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ property: 'og:title', content: page.title });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ name: 'twitter:title', content: page.title });
    this.meta.updateTag({ name: 'twitter:description', content: description });

    if (page.path) {
      const url = this.absolute(page.path);
      this.canonical(url);
      this.meta.updateTag({ property: 'og:url', content: url });
    }

    if (page.image) {
      const image = this.absolute(page.image);
      this.meta.updateTag({ property: 'og:image', content: image });
      this.meta.updateTag({ name: 'twitter:image', content: image });
    }
  }

  reset(path: string): void {
    this.setPage({ title: DEFAULT_TITLE, path });
  }

  private canonical(url: string): void {
    let link = this.document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!link) {
      link = this.document.createElement('link');
      link.rel = 'canonical';
      this.document.head.appendChild(link);
    }
    link.href = url;
  }

  private absolute(pathOrUrl: string): string {
    if (/^https?:\/\//i.test(pathOrUrl)) return pathOrUrl;
    return environment.siteUrl.replace(/\/$/, '') + '/' + pathOrUrl.replace(/^\//, '');
  }
}
