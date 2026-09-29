import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { FeatureService } from '../services/feature.service';

/** Customer Cars pages: while the site shows Trips only (appsettings Features:HideCarsAndHolidays), go home instead. */
export const carsEnabledGuard: CanActivateFn = () => {
  const router = inject(Router);
  return inject(FeatureService)
    .load()
    .pipe(map((f) => (f.hideCarsAndHolidays ? router.createUrlTree(['/']) : true)));
};
