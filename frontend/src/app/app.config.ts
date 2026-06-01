import { APP_INITIALIZER, ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { APP_ROUTES } from './app.routes';
import { AuthService } from './core/auth/auth.service';
import { authInterceptor } from './core/auth/auth.interceptor';
import { TelegramWebAppService } from './core/telegram/telegram-web-app.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(APP_ROUTES, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor])),
    {
      provide: APP_INITIALIZER,
      multi: true,
      useFactory: (tg: TelegramWebAppService, auth: AuthService) => async () => {
        tg.ready();
        // When launched inside Telegram, swap straight to the Mini App auth
        // flow so the user lands on the dashboard without seeing /login.
        // A pre-existing localStorage session (from a prior browser visit)
        // is intentionally overwritten — the Telegram identity wins inside TG.
        if (tg.isInMiniApp) {
          try {
            await auth.authenticateMiniApp(tg.initData);
          } catch {
            // Fall through to the normal login screen; rare (clock skew /
            // revoked bot / stale initData).
          }
        }
      },
      deps: [TelegramWebAppService, AuthService],
    },
  ],
};
