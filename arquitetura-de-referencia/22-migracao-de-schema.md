# Migração de schema com múltiplas réplicas (expand/contract)

> Sem fonte de discussão prévia isolada. Motivado por qualquer sistema com banco compartilhado e mais de uma réplica do processo web: mesma família de risco de qualquer estado compartilhado entre réplicas (ver [`10-configuracao-e-segredos.md`](10-configuracao-e-segredos.md), chave de criptografia de sessão).

## O problema do rolling deploy

Com mais de uma réplica atrás de um load balancer, um deploy não troca a versão em produção instantaneamente: existe uma janela em que réplicas diferentes rodam versões diferentes do código, todas contra o **mesmo** banco físico compartilhado. Uma migration que remove, renomeia, ou muda o tipo de uma coluna nesse meio-tempo quebra a réplica que ainda não atualizou. Isso é comportamento normal de qualquer rolling deploy, não um cenário raro.

## Faseamento de migration breaking

Nenhuma migration breaking acontece no mesmo deploy do código que depende dela: toda mudança de schema que remove, renomeia, muda tipo, ou adiciona coluna obrigatória sem valor default é dividida em pelo menos dois deploys:

```text
Deploy 1 — Expand
  migration: adiciona o novo elemento de schema (coluna opcional, tabela nova)
  código: ainda não usa o elemento novo

Deploy 2 — Migrate
  código: passa a ler/escrever o elemento novo
  schema: o antigo ainda existe — qualquer réplica de qualquer deploy funciona

Deploy 3 — Contract (só depois de confirmar que 100% das réplicas rodam o Deploy 2)
  migration: remove o elemento antigo
```

**Nunca** um `RENAME COLUMN` direto, nem uma coluna obrigatória adicionada sem passo intermediário de backfill: os dois quebram instantaneamente qualquer réplica que ainda não atualizou o código.

## Aprovação para migration breaking

Toda migration breaking exige aprovação explícita: mesma exigência de [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md) para a exceção de consulta multi-schema, ela passa por revisão explícita antes do merge, mesmo seguindo o padrão expand/contract. Essa aprovação é condição para o merge do Pull Request que contém a migration, não um passo que acontece depois, durante o deploy. Se a aprovação não aconteceu, o PR não é mesclado, e a migration não chega a existir em nenhum ambiente compartilhado.

## Relação com versionamento de contrato

Mesmo espírito de [`18-versionamento-de-api.md`](18-versionamento-de-api.md): mudança backward-compatible não exige processo especial; mudança breaking exige processo explícito e faseado. Lá o contrato é a API HTTP; aqui é o schema do banco, mesmo princípio aplicado à camada de persistência.

## Tabelas técnicas seguem a mesma regra

Tabelas de infraestrutura (sessão, auditoria, idempotência) são tabelas do mesmo banco: qualquer mudança breaking nelas segue o mesmo padrão expand/contract, sem exceção por serem "técnicas". Isso vale tanto para uma tabela técnica transitória (ex.: sessão, com linhas de vida curta) quanto para uma de vida longa (ex.: auditoria, retida por prazos extensos, ver [`23-retencao-e-expurgo-de-dados.md`](23-retencao-e-expurgo-de-dados.md)): o critério de "todas as réplicas já rodam o Deploy 2" para autorizar o Deploy 3 (Contract) é o mesmo independentemente da natureza da tabela. O que muda entre elas é só o volume de dado eventualmente migrado/backfillado no Deploy 2, não a regra do processo em si.

## Veja também

- [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md): banco único, motivo pelo qual esta regra é necessária
- [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md): mesma exigência de aprovação explícita, aplicada lá à exceção de consulta multi-schema
- [`18-versionamento-de-api.md`](18-versionamento-de-api.md): mesmo princípio backward-compatible por padrão, aplicado ao contrato HTTP em vez do schema
- [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md): onde um job de backfill de dados pode rodar
- [`10-configuracao-e-segredos.md`](10-configuracao-e-segredos.md): mesma família de risco (estado compartilhado entre réplicas), já vista ali para a chave de criptografia de sessão
- [`23-retencao-e-expurgo-de-dados.md`](23-retencao-e-expurgo-de-dados.md): tabelas técnicas de vida longa (ex.: auditoria), citadas aqui como exemplo de tabela técnica que segue a mesma regra de migração que uma transitória
- [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md): mesma família de risco de concorrência/janela de corrida, aqui entre réplicas durante um rolling deploy, lá entre requisições concorrentes na mesma versão do código
