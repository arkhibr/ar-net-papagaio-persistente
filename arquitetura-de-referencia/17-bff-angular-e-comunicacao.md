# BFF, Angular e como tudo se comunica

> Complementa [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md) (identidade e sessão) com a topologia de comunicação entre frontend, BFF e módulos de backend, assunto relacionado mas diferente: aquele documento resolve _quem_ é o usuário; este resolve _como_ a requisição chega até o módulo certo.

## Angular como cliente fino

O Angular é cliente fino, nunca fonte de verdade:

- não controla o protocolo de autenticação (WS-Federation, OIDC, SAML, o que quer que o IdP use);
- não armazena access token nem refresh token;
- não conhece endpoints internos de sistemas legados/externos;
- não determina a identidade confiável do usuário nem o papel/permissão que o usuário carrega: usa o que o backend devolve só para UX (esconder/mostrar botão), nunca como mecanismo de autorização (ver [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md)).

O BFF é a fronteira de segurança entre o navegador e a aplicação, não o Angular.

## Mesma origem entre Angular e backend

Angular e backend operam sob a mesma origem em produção, sem CORS. Rotas:

```text
/
  Angular (SPA)

/auth/*
  autenticação, login, logout, sessão

/api/*
  funcionalidades de negócio
```

Isso elimina CORS como preocupação de arquitetura: o navegador nunca faz requisição cross-origin para a aplicação. `Angular` usa `HttpClient` com `withCredentials`/cookie automático, sem interceptor de token, sem biblioteca OIDC no cliente.

## Topologia do BFF

BFF é um **papel** (terminar o fluxo de autenticação, guardar token só no servidor, expor sessão via cookie, mediar CSRF), não necessariamente um componente físico separado, e não implica um processo físico separado do backend. Duas topologias válidas, escolhidas pelo mesmo critério de [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md):

```text
Topologia A: BFF e backend no mesmo processo (monólito modular)

Angular
   │ HTTP, mesma origem, cookie
   ▼
Api (papel de BFF: Auth + Session + CSRF, e também composition root do backend)
   │ ISender.Send(command | query), in-process
   ▼
Módulo (Application → Domain)
```

```text
Topologia B: BFF fisicamente separado de um backend com múltiplos deployables

Angular
   │ HTTP, mesma origem, cookie
   ▼
BFF (processo próprio: Auth + Session + CSRF + proxy reverso, ex.: YARP)
   │ HTTP/gRPC, Authorization: Bearer anexado no servidor
   ▼
Api de cada backend (deployable independente)
```

**Regra direta: não force a Topologia B quando o backend inteiro é um único monólito modular.** Introduzir um BFF fisicamente separado de um backend que só tem um deployable adiciona, sem necessidade, HTTP interno, autenticação entre serviços, mais um deployable, propagação de contexto: exatamente os custos que [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md) evita para comunicação _dentro_ do monólito, agora reintroduzidos na fronteira externa sem ganho correspondente. Some a isso o custo operacional de rastrear uma requisição que atravessa mais um processo: tracing distribuído (ver [`15-observabilidade.md`](15-observabilidade.md)) deixa de ser opcional e passa a ser exigido só para manter a mesma capacidade de diagnóstico que a Topologia A tem de graça. A Topologia B só se justifica quando o backend precisa existir como produto independente, consumido por múltiplos canais/aplicações além deste frontend Angular específico, ou quando módulos já foram efetivamente extraídos como deployables próprios (ver critério de promoção em [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md)).

## Granularidade do BFF

Um BFF por aplicação, nunca um BFF por módulo/domínio:

```text
Não fazer:
  Módulo A BFF
  Módulo B BFF
  Módulo C BFF

Fazer:
  Aplicação
      ↓
  BFF (um só, desta aplicação)
      ↓
  Módulo A / Módulo B / Módulo C
```

BFF é orientado à experiência de frontend (uma aplicação, uma sessão, um conjunto de telas); módulos são orientados ao negócio. Um frontend genuinamente diferente, de outra aplicação, pode justificar outro BFF, mas não justifica um módulo a mais dentro da mesma aplicação.

## Onde a requisição cruza para dentro do backend

```text
Angular
   │ POST /api/{recurso}/{acao}
   │ Cookie de sessão + header antiforgery
   ▼
Api (BFF)
   ├── valida sessão → ICurrentUser (ver 11-autenticacao-e-sessao.md)
   ├── valida CSRF
   ▼
ISender.Send(Command)
   ├── Logging → Validation → Authorization → Idempotency → Caching → Handler (ver 03-commands-e-queries.md)
   ▼
Handler do módulo dono
   │ se precisar de outro módulo: Contracts, nunca DbContext alheio (ver 04-comunicacao-entre-modulos.md)
   ▼
Domain
```

A partir do momento em que a requisição entra no `Api`, a continuação do fluxo é coberta por [`03-commands-e-queries.md`](03-commands-e-queries.md), que descreve o pipeline completo (Logging → Validation → Authorization → Idempotency → Caching → Handler) e, se a requisição cruzar módulo, por [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md). Este documento cobre só o trecho Angular → Api, não repete o que acontece depois.

**Caso específico: uma tela precisa de dado de mais de um módulo na mesma resposta.** O `Api` (papel de BFF) pode compor o resultado de mais de uma `Query`, cada uma para o módulo dono do respectivo dado, nunca acessando o `DbContext` de um módulo diretamente. Regra completa, incluindo onde essa composição mora e quando essa composição deveria virar um read model dedicado em vez de composição ad hoc, em [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md), seção "Composição na borda".

## CSRF

Como o cookie de sessão é enviado automaticamente pelo navegador, toda operação mutável (`POST`, `PUT`, `PATCH`, `DELETE`) precisa de proteção CSRF. O Angular usa o mecanismo XSRF nativo do `HttpClient` (lê um cookie não-`HttpOnly` com o token, devolve num header customizado), com validação correspondente no backend. A responsabilidade da proteção é do backend, ainda que o Angular participe do envio do token.

## Contrato de erro no cliente

O Angular consome o mesmo contrato de status HTTP de [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md) através de um único interceptor HTTP, não tratamento ad hoc por chamada: `401` redireciona para login, `403` mostra acesso negado (nunca um erro genérico indistinguível de bug), `409` de idempotência/concorrência informa que a operação já está em andamento ou foi alterada por outra sessão, `400` sempre itera o array `errors[]` do corpo (nunca `detail` isolado, ver `06`) e mapeia cada item para o campo do formulário indicado por `pointer` — ou para uma mensagem geral do formulário quando `pointer` vem nulo (violação de regra de negócio, não de um campo específico). Como o formato de `errors[]` é o mesmo para um erro de validação sintática ou uma única violação de domínio, o interceptor não precisa saber qual exceção o backend lançou. Centralizar isso num interceptor evita que cada tela reimplemente sua própria lógica de tratamento de erro HTTP.

Falha de validação do token CSRF é tratada separadamente desse contrato de status de negócio: o backend responde com `403` quando o header antiforgery está ausente ou inválido, e o interceptor trata esse caso como sessão inválida (renovando o token e orientando nova tentativa), não como acesso negado de autorização de recurso.

## O que o Angular pode saber sobre a sessão

Um endpoint técnico (`GET /api/me`) devolve o necessário para a UX (nome, papéis para esconder/mostrar elementos de tela), nunca como mecanismo de autorização real. Esconder um botão no Angular não impede a chamada HTTP direta ao endpoint; a autorização de verdade acontece no backend (ver [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md)).

## Veja também

- [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md): identidade, sessão, `ICurrentUser`, `SecurityVersion`: o que acontece antes deste documento na cadeia de confiança
- [`03-commands-e-queries.md`](03-commands-e-queries.md): pipeline a partir do momento em que a requisição entra no `Api`
- [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md): o que acontece se o `Command`/`Query` cruzar módulo
- [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md): contrato de status HTTP consumido pelo interceptor do Angular
- [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md): por que esconder um botão no Angular não é autorização
- [`28-push-em-tempo-real.md`](28-push-em-tempo-real.md): o canal assíncrono servidor→cliente que complementa o request/response deste documento
