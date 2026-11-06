import { ApplicationConfig, inject, provideAppInitializer, provideZoneChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';
import { idempotenciaInterceptor } from './core/interceptors/idempotencia.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { SessionService } from './core/services/session.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([idempotenciaInterceptor, errorInterceptor]),
      // Mesmos nomes do backend (Bff/Csrf.cs, XsrfCookie). Explícitos porque precisam bater
      // (adaptacao-bff-angular.md, F4).
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    // A sessão vem de GET /api/me antes do primeiro roteamento, então os guards podem ser
    // síncronos (F1/F2).
    provideAppInitializer(() => firstValueFrom(inject(SessionService).carregar())),
  ],
};
