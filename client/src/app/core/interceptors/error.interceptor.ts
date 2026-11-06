import { inject } from '@angular/core';
import { HttpClient, HttpContext, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { EMPTY, catchError, throwError } from 'rxjs';
import { ProblemDetails } from '../models/problem-details.model';
import { SEM_TRATAMENTO_GLOBAL_DE_ERRO } from '../http/contexto';
import { SessionService } from '../services/session.service';
import { NAVEGACAO_DE_AUTENTICACAO } from '../services/navegacao-de-autenticacao';

/**
 * Tratamento único do contrato de erro (arquitetura/17, "Contrato de erro no cliente";
 * adaptacao-bff-angular.md, F5). 401 leva ao login do backend; 403 de CSRF é sessão a renovar,
 * não acesso negado.
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.context.get(SEM_TRATAMENTO_GLOBAL_DE_ERRO)) {
    return next(req);
  }

  const router = inject(Router);
  const snackBar = inject(MatSnackBar);
  const http = inject(HttpClient);
  const session = inject(SessionService);
  const navegacao = inject(NAVEGACAO_DE_AUTENTICACAO);

  return next(req).pipe(
    catchError((erro: HttpErrorResponse) => {
      if (erro.status === 401) {
        session.limpar();
        navegacao.irParaLogin(router.url);
        return EMPTY;
      }

      const problema: ProblemDetails = erro.error?.status
        ? (erro.error as ProblemDetails)
        : { type: 'about:blank', title: 'Erro desconhecido', status: erro.status };

      if (ehFalhaDeCsrf(problema)) {
        // Renova o token e orienta nova tentativa; não reenvia sozinho (arquitetura/17).
        http
          .get('/auth/csrf', { context: new HttpContext().set(SEM_TRATAMENTO_GLOBAL_DE_ERRO, true) })
          .subscribe({ error: () => undefined });
        snackBar.open('Sua sessão foi renovada. Tente a ação de novo.', 'Fechar', { duration: 5000 });
        return throwError(() => problema);
      }

      const mensagem = mensagemPorStatus(erro.status, problema);
      if (mensagem) {
        snackBar.open(mensagem, 'Fechar', { duration: 5000 });
      }
      return throwError(() => problema);
    }),
  );
};

export function ehFalhaDeCsrf(problema: ProblemDetails): boolean {
  return problema.status === 403 && problema.type.endsWith('/csrf-invalido');
}

/**
 * Mensagem global por status (contratos-front.md, seção 3). 400 e 404 não têm mensagem global:
 * a tela mostra o erro no próprio formulário ou no lugar do conteúdo.
 */
function mensagemPorStatus(status: number, problema: ProblemDetails): string | null {
  switch (status) {
    case 403:
      return 'Acesso negado: você não tem permissão para esta ação.';
    case 409:
      return 'Conflito: a operação já está em andamento ou o recurso foi alterado. Confira e tente de novo.';
    case 412:
      return 'Este chamado foi alterado por outra pessoa desde que você o abriu. Os dados foram recarregados.';
    case 422:
      return 'Não foi possível concluir a ação. Tente de novo.';
    case 429:
      return 'Muitas tentativas. Aguarde um minuto e tente de novo.';
    default:
      return status >= 500
        ? `Erro interno no servidor. Tente novamente.${problema.traceId ? ` (código: ${problema.traceId})` : ''}`
        : null;
  }
}
