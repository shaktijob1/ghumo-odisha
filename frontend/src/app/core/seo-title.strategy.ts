import { Injectable, inject } from '@angular/core';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { SeoService } from './services/seo.service';

/**
 * Runs on every navigation: routes with a `title` get it, every other page goes back to the site
 * default (so a title from the previous page never lingers). Trip and destination pages then set
 * their own title once their data loads.
 */
@Injectable({ providedIn: 'root' })
export class SeoTitleStrategy extends TitleStrategy {
  private readonly seo = inject(SeoService);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const path = snapshot.url.split(/[?#]/)[0] || '/';
    const title = this.buildTitle(snapshot);
    if (title) {
      this.seo.setPage({ title, path });
    } else {
      this.seo.reset(path);
    }
  }
}
