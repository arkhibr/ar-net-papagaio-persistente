import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionService } from '../services/session.service';

/**
 * Só UX: esconde a rota de quem não tem o papel. A autorização real é do backend
 * (arquitetura/12; [Authorize(Roles)] e IsAuthorizedAsync), que responde 403 de qualquer jeito.
 */
export function roleGuard(papeisPermitidos: string[]): CanActivateFn {
  return () => {
    const session = inject(SessionService);
    const router = inject(Router);

    if (papeisPermitidos.some((papel) => session.hasRole(papel))) {
      return true;
    }

    return router.createUrlTree(['/chamados/meus']);
  };
}
