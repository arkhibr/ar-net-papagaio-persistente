import { InjectionToken } from '@angular/core';

/**
 * Navegação de página inteira para a autenticação do backend (arquitetura/17: o Angular não
 * controla o protocolo de autenticação; adaptacao-bff-angular.md, F3). O login acontece em
 * /auth/login, servido pelo BFF, hoje com o provedor local e depois com o IdP, sem mudar nada
 * aqui. Fica atrás de um token de injeção para os testes substituírem o window.location.
 */
export interface NavegacaoDeAutenticacao {
  irParaLogin(returnUrl: string): void;
  irPara(url: string): void;
}

export const NAVEGACAO_DE_AUTENTICACAO = new InjectionToken<NavegacaoDeAutenticacao>('NAVEGACAO_DE_AUTENTICACAO', {
  providedIn: 'root',
  factory: () => ({
    irParaLogin: (returnUrl: string) =>
      window.location.assign('/auth/login?returnUrl=' + encodeURIComponent(returnUrl)),
    irPara: (url: string) => window.location.assign(url),
  }),
});
