import { Injectable, inject } from '@angular/core';
import { ActivatedRouteSnapshot, RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { SeoService } from './services/seo.service';

/**
 * Runs on every navigation. Public pages get their title, description, canonical link and
 * structured data from the API (the same values the server wrote into the first response);
 * routes marked `data: { noindex: true }` (account, admin, driver, sign-in) just get their own
 * title and a noindex rule.
 */
@Injectable({ providedIn: 'root' })
export class SeoTitleStrategy extends TitleStrategy {
  private readonly seo = inject(SeoService);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const path = snapshot.url.split(/[?#]/)[0] || '/';
    const title = this.buildTitle(snapshot) ?? null;
    if (this.isNoIndex(snapshot.root)) {
      this.seo.updatePrivatePage(title);
    } else {
      this.seo.updatePublicPage(path, title);
    }
  }

  private isNoIndex(route: ActivatedRouteSnapshot): boolean {
    for (let r: ActivatedRouteSnapshot | null = route; r; r = r.firstChild) {
      if (r.data['noindex']) return true;
    }
    return false;
  }
}
