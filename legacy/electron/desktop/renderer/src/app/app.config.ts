import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withHashLocation } from '@angular/router';

import { routes } from './app.routes';

/**
 * Root application providers. `withHashLocation()` because the packaged app is
 * served over `file://` — routes live in the URL fragment and resolve purely
 * client-side. `eventCoalescing` batches DOM events (slider drags!) into fewer
 * change-detection passes.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withHashLocation()),
  ],
};
