# Commands e Queries via Mediator

## Regra direta

Cada módulo despacha Commands/Queries via `Mediator` (`ISender`), compostos por pipeline behaviors, em vez de handlers chamados diretamente pelo controller:

```text
Api (Controller)
   ↓ ISender.Send(command | query)
{Modulo}.Application (Handler)
   ↓
{Modulo}.Domain
```

## Nomenclatura de Command/Query

Command e Query seguem sufixo por tipo de artefato: `{CasoDeUso}Command` para o que muda estado, `{CasoDeUso}Query` para o que só lê, nunca o inverso, e nunca um nome genérico como `ProcessarCommand` que não diz qual caso de uso está sendo executado. `{CasoDeUso}` é vocabulário de domínio, na língua do negócio; `Command`/`Query` são termo técnico, em inglês, mesma regra de [`08-convencao-de-nomenclatura.md`](08-convencao-de-nomenclatura.md) aplicada a este artefato específico, sem exceção.

## Imutabilidade da mensagem

**Regra direta: `Command`, `Query` e DTO são tipos imutáveis (`record`).** A mensagem que trafega pelo pipeline não muda no caminho — é dado, não objeto com comportamento. É o corolário de imutabilidade do núcleo funcional (ver [`02-dominio-hibrido.md`](02-dominio-hibrido.md), "Núcleo funcional, casca imperativa").

## Ordem fixa dos pipeline behaviors

```text
Logging → Validation → Authorization → Idempotency → Caching → Handler
```

Um behavior novo entra na posição que a natureza dele exige: autorização depois de `Validation` (não vale gastar uma checagem de permissão numa entrada mal formada) e antes de `Idempotency` (uma tentativa não autorizada não deveria reservar uma chave de idempotência).

## Command/Query vs. chamada direta a serviço

Não existe uma regra única para a solution inteira: os dois estilos coexistem lado a lado, escolhidos conforme o caso de uso.

- **Command/Query** quando o caso de uso muda estado de um agregado, recebe entrada que precisa de validação, ou é razoável esperar um cross-cutting concern (log de auditoria, cache, autorização por regra de negócio) hoje ou no futuro próximo.
- **Chamada direta a serviço** quando é leitura simples, sem parâmetro relevante (ou quase), sem regra de negócio real por trás, sem expectativa de crescer em complexidade.
- **Promova de serviço direto para Command/Query** no momento em que a exceção deixar de ser exceção: se um serviço "simples" ganha parâmetro que precisa de validação, ou alguém pede log/cache específico para ele, esse serviço deveria estar no pipeline, não fora dele.

## Biblioteca de Mediator

**Regra direta: a biblioteca de Mediator é a `martinothamar/Mediator` (source-generated), não a MediatR.** Dois motivos, ambos concretos:

- **Licença.** A MediatR passou a adotar licenciamento comercial (as versões gratuitas foram congeladas). Uma referência corporativa nova não deveria nascer presa a uma dependência com custo de licença por organização quando existe alternativa gratuita equivalente.
- **AOT/trimming.** A `martinothamar/Mediator` resolve handlers e pipeline behaviors por **source generator**, compatível com trimming e Native AOT. A MediatR resolve por reflection/assembly scanning, o principal bloqueador de AOT. Mesmo que AOT não seja meta hoje, a escolha não fecha essa porta.

Consequência nas assinaturas usadas em todos os exemplos desta referência: handler é `IRequestHandler<TRequest, TResponse>` com `public ValueTask<TResponse> Handle(...)` (não `Task`); notification handler é `INotificationHandler<T>` com `ValueTask Handle(...)`; behavior é `IPipelineBehavior<TMessage, TResponse>` com `MessageHandlerDelegate<TMessage, TResponse> next` e retorno `ValueTask`; despacho por `ISender.Send`, publicação por `IPublisher.Publish`; o tipo `Unit` vem da própria biblioteca. Como handlers são descobertos por source generator (não por reflection), o registro em DI dispensa assembly scanning.

## O que muda num monólito modular

Num monólito modular, um `Command`/`Query` pode cruzar módulo: ele pode ser despachado por outro módulo, através do `Contracts` do módulo dono. Esse é o próprio mecanismo de comunicação entre módulos (ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)). O handler continua morando sempre no módulo dono, nunca em quem chama.

## Veja também

- [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md): despacho de Command/Query entre módulos
- [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md): `Result.Failure` vs. exceção, tabela de status HTTP
- [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md): onde o behavior de autorização se encaixa nesta ordem
- [`08-convencao-de-nomenclatura.md`](08-convencao-de-nomenclatura.md): sufixo de Command/Query e critério de língua de domínio vs. termo técnico
- [`24-cache.md`](24-cache.md): o `CachingBehavior` desta ordem, opt-in por `Query`
- [`25-transacao-e-unit-of-work.md`](25-transacao-e-unit-of-work.md): o `UnitOfWorkBehavior` como envelope transacional do handler
