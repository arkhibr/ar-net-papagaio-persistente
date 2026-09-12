# Feature flags e rollout gradual (padrão opcional)

> Diferente dos demais documentos desta pasta, este não registra uma regra obrigatória: documenta um padrão **disponível**, para adotar quando um sistema precisar de rollout progressivo ou kill switch, não como requisito de toda funcionalidade nova. Complementa [`18-versionamento-de-api.md`](18-versionamento-de-api.md): versionamento protege o contrato quando ele muda; feature flag controla quem vê qual implementação por trás do **mesmo** contrato.

## Quando vale a pena adotar, e quando não

**Vale a pena quando:**

- a mudança tem risco/blast radius alto o suficiente para justificar liberar por etapas;
- é preciso um kill switch para desligar uma funcionalidade nova rapidamente, sem depender de um novo deploy;
- o deploy do código precisa acontecer antes da decisão de negócio de lançar ou não.

**Não vale por padrão:** introduzir flag para toda funcionalidade nova é cerimônia sem retorno, mesmo espírito de [`03-commands-e-queries.md`](03-commands-e-queries.md) ("promova de serviço direto para Command só quando a exceção deixar de ser exceção"). Uma flag nunca decidida e removida vira dívida técnica: um segundo caminho de código que ninguém mais testa.

## Onde a decisão da flag mora, se adotada

A decisão da flag **nunca mora no `Domain`**, pelo mesmo raciocínio aplicado ao `TimeProvider` ([`13-estrategia-de-testes.md`](13-estrategia-de-testes.md)): flag é estado ambiente, não regra de negócio.

| Granularidade | Onde resolve |
| --- | --- |
| Kill switch de uma funcionalidade inteira | `Api` (middleware, ou início da action) — resposta explícita (404/503), não comportamento silenciosamente diferente |
| Variação de comportamento dentro de um caso de uso existente | `Handler` da `Application`, via abstração injetada (ex.: `IFeatureManager`), nunca lendo configuração diretamente no handler (ver [`10-configuracao-e-segredos.md`](10-configuracao-e-segredos.md)) |

## Biblioteca de referência

`Microsoft.FeatureManagement` (gratuita, MIT). Integra com `IConfiguration`, suporta filtro de porcentagem/targeting e middleware nativo do ASP.NET Core.

## Onde o valor da flag vive

Não é segredo, mas é configuração que precisa mudar sem novo deploy. Por isso é candidata natural ao provedor de configuração dinâmica já citado em [`10-configuracao-e-segredos.md`](10-configuracao-e-segredos.md), reservado para quando "a necessidade for real". Feature flag, quando adotada, é exatamente essa necessidade.

## Convenção de nome

`{Modulo}.{Funcionalidade}`. Chave técnica de configuração: identifica o módulo dono e o que a flag controla, mesmo não sendo vocabulário de domínio no sentido estrito de [`08-convencao-de-nomenclatura.md`](08-convencao-de-nomenclatura.md).

## Ciclo de vida da flag

Se adotada, toda flag tem prazo: nasce com um critério de remoção definido, decidida e removida do código num prazo determinado, nunca deixada indefinidamente "por via das dúvidas".

## Veja também

- [`18-versionamento-de-api.md`](18-versionamento-de-api.md): diferença entre versionar contrato e controlar rollout de implementação
- [`10-configuracao-e-segredos.md`](10-configuracao-e-segredos.md): onde o valor da flag vive
- [`03-commands-e-queries.md`](03-commands-e-queries.md): mesmo critério de não adotar cedo demais
- [`13-estrategia-de-testes.md`](13-estrategia-de-testes.md): `TimeProvider`, mesmo raciocínio de não deixar o `Domain` ler estado ambiente
