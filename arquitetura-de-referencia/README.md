# Arquitetura de referência corporativa: índice

Esta pasta consolida recomendações arquiteturais **genéricas**, não vinculadas a um sistema específico. Um sistema concreto pode adotar uma recomendação daqui como está ou divergir conscientemente dela, desde que registre o motivo. Cada documento numerado traz, quando aplicável, uma seção "Nota de aplicação" que registra como o sistema aplica a regra ou diverge dela e por quê, de modo que a divergência não seja confundida com inconsistência não percebida.

## Documentos

| Arquivo | Conteúdo |
| --- | --- |
| [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md) | Quantos projetos por módulo num monólito modular C#/.NET, como garantir a fronteira entre módulos, e critério de quando promover um módulo para estrutura completa |
| [`02-dominio-hibrido.md`](02-dominio-hibrido.md) | Domínio rico vs. anêmico; fronteira de agregado como fronteira de módulo |
| [`03-commands-e-queries.md`](03-commands-e-queries.md) | Mediator/CQRS, ordem de pipeline behaviors, Command/Query vs. serviço direto |
| [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md) | Como um módulo lê e escreve dado de outro: nunca `DbContext` direto, chamada a `Contracts`, projeção via fila para CQRS completo, exceção de schema cruzado |
| [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md) | Dispatch in-process para orquestração entre módulos vs. Outbox/worker para saída de processo |
| [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md) | `Result.Failure` vs. exceção, `Idempotency-Key`, `RowVersion`, tabela exceção→status HTTP, RFC 9457 e `errors[]` |
| [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md) | Anti-Corruption Layer para sistemas externos/legados, resiliência obrigatória, identidade técnica vs. identidade do ator |
| [`08-convencao-de-nomenclatura.md`](08-convencao-de-nomenclatura.md) | Vocabulário de domínio na língua do negócio, termo técnico em inglês, prefixo do sistema segue a mesma regra |
| [`09-convencoes-rest-api.md`](09-convencoes-rest-api.md) | Rotas na língua de domínio, verbo HTTP por operação, nunca `PUT`/`PATCH` genérico |
| [`10-configuracao-e-segredos.md`](10-configuracao-e-segredos.md) | `IOptions<T>`, tabela de segredos, chave de criptografia de sessão externalizada e compartilhada entre réplicas |
| [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md) | Sessão server-side, identidade interna canônica, `ICurrentUser`, `SecurityVersion`, BFF, agnóstico de protocolo de IdP |
| [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md) | `IRequiresAuthorization`/`AuthorizationBehavior` para autorização dependente de dado, fora do agregado |
| [`13-estrategia-de-testes.md`](13-estrategia-de-testes.md) | Projeto de teste por camada/módulo, teste arquitetural de fronteira entre módulos |
| [`14-filtro-de-dados.md`](14-filtro-de-dados.md) | Filtro por linha (Specification/Global Query Filter) e por campo (DTO por audiência/redação) |
| [`15-observabilidade.md`](15-observabilidade.md) | Correlação entre processos, log estruturado, nível de log, critério sistêmico (não temporal) para adotar OpenTelemetry |
| [`16-validacao-sintatica-vs-invariante.md`](16-validacao-sintatica-vs-invariante.md) | Validação de forma vs. invariante de negócio vs. autorização — três categorias, fácil confundir a terceira com as outras duas |
| [`17-bff-angular-e-comunicacao.md`](17-bff-angular-e-comunicacao.md) | Angular como cliente fino, BFF como papel (não necessariamente componente físico separado), mesma origem, CSRF, contrato de erro no cliente |
| [`18-versionamento-de-api.md`](18-versionamento-de-api.md) | Versionamento por segmento de URL, critério de breaking change, depreciação, versionamento como fronteira de migração incremental |
| [`19-paginacao.md`](19-paginacao.md) | Página/tamanho vs. cursor, envelope de resposta, limite máximo de página, ordenação, ordem de aplicação com filtro de dados |
| [`20-feature-flags.md`](20-feature-flags.md) | Padrão **opcional** de rollout gradual/kill switch — quando adotar, onde a decisão mora, biblioteca de referência, ciclo de vida da flag |
| [`21-auditoria.md`](21-auditoria.md) | Mecanismo de auditoria: marcador `IAuditable` em evento de domínio, gravação na mesma transação via interceptor, de onde vem o contexto do ator |
| [`22-migracao-de-schema.md`](22-migracao-de-schema.md) | Padrão expand/contract para migration breaking, compatível com rolling deploy de múltiplas réplicas |
| [`23-retencao-e-expurgo-de-dados.md`](23-retencao-e-expurgo-de-dados.md) | Política de retenção por tipo de dado, anonimização seletiva quando há obrigação legal de retenção, acesso/eliminação como orquestração entre módulos |
| [`24-cache.md`](24-cache.md) | Cache opt-in por `Query` (`ICacheableQuery`), chave sempre com escopo do ator, invalidação por tag na transação de escrita, `HybridCache` |
| [`25-transacao-e-unit-of-work.md`](25-transacao-e-unit-of-work.md) | Uma transação/commit por `Command` no `UnitOfWorkBehavior`, atomicidade com auditoria/Outbox/idempotência, disparo pós-commit de eventos de domínio, `ConcurrencyException` |
| [`26-background-jobs-e-worker.md`](26-background-jobs-e-worker.md) | Operação do worker: hospedagem, consumo at-least-once do Outbox, retry/backoff, dead-letter, jobs agendados, identidade de sistema |
| [`27-leitura-e-query-side.md`](27-leitura-e-query-side.md) | Lado de leitura do CQRS dentro do módulo: `AsNoTracking`, projeção `.Select` no banco, DTO de leitura, quando usar Dapper/compiled queries |
| [`28-push-em-tempo-real.md`](28-push-em-tempo-real.md) | Push servidor→cliente (SignalR/SSE): dica e não fonte de verdade, best-effort, `IUserNotifier`, escopo por ator, backplane em multi-réplica, conexão pela sessão |
| [`29-modelagem-e-dados-entre-modulos.md`](29-modelagem-e-dados-entre-modulos.md) | Modelagem entre módulos: referência por id vs. snapshot vs. cópia sincronizada, invariante dentro da fronteira de consistência, invariante entre agregados, integridade sem FK cross-schema, ACL/context-mapping interno |

Documento de revisão (não é regra de arquitetura, é índice de trabalho pendente):

| Arquivo | Conteúdo |
| --- | --- |
| [`00-revisao-e-lacunas.md`](00-revisao-e-lacunas.md) | Revisão da referência contra padrões recentes de .NET/C#: decisões tomadas, defasagens, lacunas de escopo e inconsistências |

## Princípios fundamentais

Alguns princípios estruturais valem para toda a referência e são aplicados em vários documentos. Em vez de um manifesto separado (que derivaria do código), cada um mora junto do concern que governa e, quando possível, é verificado como fitness function. Índice de onde cada um está enunciado:

| Princípio | Onde está enunciado | Verificado por |
| --- | --- | --- |
| Ports & Adapters / inversão de dependência (seta para dentro, porta pertence a quem consome, injeção por construtor) | [`01`](01-estrutura-de-projetos-monolito-modular.md), "Regra de dependência" | fitness function de direção de dependência ([`13`](13-estrategia-de-testes.md)) |
| Fronteira de módulo e dependências acíclicas | [`01`](01-estrutura-de-projetos-monolito-modular.md), [`04`](04-comunicacao-entre-modulos.md) | fitness functions de fronteira e aciclicidade ([`13`](13-estrategia-de-testes.md)) |
| Fitness functions arquiteturais (toda regra automatizável vira teste) | [`13`](13-estrategia-de-testes.md), "Fitness functions arquiteturais" | as próprias, em CI |
| Núcleo funcional, casca imperativa (domínio puro; efeito na borda; mensagem imutável) | [`02`](02-dominio-hibrido.md), "Núcleo funcional"; consequência em [`13`](13-estrategia-de-testes.md) e [`03`](03-commands-e-queries.md) | fitness function de pureza do `Domain` ([`13`](13-estrategia-de-testes.md)) |
| Adiar a decisão até o sinal aparecer (promoção por sinal objetivo, não antecipação) | [`01`](01-estrutura-de-projetos-monolito-modular.md), [`05`](05-processamento-assincrono-e-eventos.md), [`20`](20-feature-flags.md), [`24`](24-cache.md) | — |
