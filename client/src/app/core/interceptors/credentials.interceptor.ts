import { HttpInterceptorFn } from '@angular/common/http';

const ROTAS_MUTAVEIS_IDEMPOTENTES: RegExp[] = [
  /^\/api\/v1\/chamados$/,
  /^\/api\/v1\/chamados\/[^/]+\/atribuir$/,
  /^\/api\/v1\/chamados\/[^/]+\/devolver$/,
  /^\/api\/v1\/chamados\/[^/]+\/reclassificar$/,
  /^\/api\/v1\/chamados\/[^/]+\/resolver$/,
  /^\/api\/v1\/chamados\/[^/]+\/fechar$/,
  /^\/api\/v1\/chamados\/[^/]+\/reabrir$/,
];

export const credentialsInterceptor: HttpInterceptorFn = (req, next) => {
  let requisicao = req.clone({ withCredentials: true });

  const caminho = new URL(requisicao.url, window.location.origin).pathname;
  const precisaIdempotencyKey =
    requisicao.method === 'POST' &&
    ROTAS_MUTAVEIS_IDEMPOTENTES.some((padrao) => padrao.test(caminho)) &&
    !requisicao.headers.has('Idempotency-Key');

  if (precisaIdempotencyKey) {
    requisicao = requisicao.clone({ setHeaders: { 'Idempotency-Key': crypto.randomUUID() } });
  }

  return next(requisicao);
};
