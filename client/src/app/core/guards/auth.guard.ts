import { inject } from '@angular/core';
import { CanActivateFn } from '@angular/router';
import { SessionService } from '../services/session.service';
import { NAVEGACAO_DE_AUTENTICACAO } from '../services/navegacao-de-autenticacao';

/**
 * Pode ser síncrono: o provideAppInitializer só libera o roteamento depois de GET /api/me
 * responder (adaptacao-bff-angular.md, F2). Sem sessão, navegação de página inteira para o login
 * do backend, que volta para a URL pedida (F3).
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const session = inject(SessionService);

  if (session.currentUser()) {
    return true;
  }

  inject(NAVEGACAO_DE_AUTENTICACAO).irParaLogin(state.url);
  return false;
};
