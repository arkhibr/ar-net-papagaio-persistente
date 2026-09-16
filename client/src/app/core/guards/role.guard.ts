import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from '../services/session.service';

export function roleGuard(papeisPermitidos: string[]): CanActivateFn {
  return () => {
    const session = inject(SessionService);
    const router = inject(Router);
    const usuario = session.currentUser();

    if (usuario && papeisPermitidos.some((papel) => usuario.roles.includes(papel))) {
      return true;
    }

    return router.createUrlTree(['/chamados/meus']);
  };
}
