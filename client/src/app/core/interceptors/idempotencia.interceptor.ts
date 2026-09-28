import { HttpInterceptorFn } from '@angular/common/http';
import { isDevMode } from '@angular/core';

const ROTAS_MUTAVEIS_IDEMPOTENTES: RegExp[] = [
  /^\/api\/v1\/chamados$/,
  /^\/api\/v1\/chamados\/[^/]+\/atribuir$/,
  /^\/api\/v1\/chamados\/[^/]+\/devolver$/,
  /^\/api\/v1\/chamados\/[^/]+\/reclassificar$/,
  /^\/api\/v1\/chamados\/[^/]+\/resolver$/,
  /^\/api\/v1\/chamados\/[^/]+\/fechar$/,
  /^\/api\/v1\/chamados\/[^/]+\/reabrir$/,
];

/**
 * A Idempotency-Key é gerada pela tela, uma vez por ação do usuário (arquitetura/06;
 * contratos-front.md, seção 8; adaptacao-bff-angular.md, F6). Este interceptor não gera chave:
 * em desenvolvimento, só aponta a rota idempotente que saiu sem ela.
 */
export const idempotenciaInterceptor: HttpInterceptorFn = (req, next) => {
  if (isDevMode() && req.method === 'POST' && !req.headers.has('Idempotency-Key')) {
    const caminho = new URL(req.url, window.location.origin).pathname;
    if (ROTAS_MUTAVEIS_IDEMPOTENTES.some((padrao) => padrao.test(caminho))) {
      console.error(`POST ${caminho} saiu sem Idempotency-Key: a tela precisa gerar a chave da ação.`);
    }
  }

  return next(req);
};
