# Auditoria

> Sem fonte de discussão prévia isolada — formaliza um mecanismo para um requisito comum a sistemas corporativos com operações sensíveis (dado financeiro, dado pessoal, decisão administrativa), sem depender de nenhuma tecnologia de auditoria de terceiros. Complementa [`15-observabilidade.md`](15-observabilidade.md), que já distingue auditoria de log técnico sem definir o mecanismo.

## Schema mínimo

Um registro de auditoria precisa, no mínimo, responder quem fez, o quê, quando, sobre qual recurso, com qual estado antes/depois, e de qual sessão/correlação:

```text
RegistroAuditoria
  Id
  OccurredAt
  ActorUserId
  SessionId
  Action
  ResourceType
  ResourceId
  OldValue
  NewValue
  Reason
  CorrelationId
  SourceIp
```

## Marcação do evento auditável

Auditoria é opt-in por evento de domínio, via marcador `IAuditable`: nem todo evento de domínio precisa de auditoria formal, só operações consideradas críticas para o domínio (regra de negócio administrativa, dado sensível, decisão irreversível). O agregado marca o evento correspondente:

```csharp
public interface IAuditable
{
    string Acao { get; }
    string TipoRecurso { get; }
    Guid RecursoId { get; }
    string? ValorAnterior { get; }
    string? ValorNovo { get; }
    string? Motivo { get; }
}
```

O agregado continua sem saber que auditoria existe: só descreve o que mudou, na própria linguagem do evento de domínio que já levantaria de qualquer forma. `OldValue`/`NewValue` vêm de conhecimento que só o agregado tem; nenhum mecanismo genérico infere isso por reflection.

## Atomicidade da gravação

O registro de auditoria grava na mesma transação da operação de negócio, não pós-commit. Diferente do dispatch de notificação entre módulos ("melhor esforço, loga e segue"), perder um registro de auditoria de uma operação crítica não é aceitável. Registrar a auditoria é o próprio requisito da operação, não uma otimização sobre ela. Como o registro vai para o mesmo banco físico da operação, isso se resolve sem Outbox: grava na mesma chamada de `SaveChanges`, na mesma transação. Notificar um sistema externo ou outro módulo cruza fronteira de processo (ver [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md)); auditoria é apenas mais uma escrita no mesmo banco, com atomicidade garantida pela própria transação e sem infraestrutura extra.

Implementado como interceptor de `SaveChanges` do ORM (`ISaveChangesInterceptor` no EF Core, ou equivalente), não como sobrescrita direta do `DbContext`: mantém a persistência focada, com a lógica de auditoria isolada e testável à parte.

Consequência direta de gravar na mesma transação: auditoria só grava operação que de fato commitou. Quando uma gravação falha por conflito de concorrência otimista (`DbUpdateConcurrencyException`/`RowVersion`, ver [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md)), o rollback da transação desfaz junto a operação de negócio e o registro de auditoria daquela tentativa. Não existe registro de auditoria de uma tentativa que falhou por concorrência.

## Onde o contexto do ator entra

O evento de domínio não sabe quem é o ator: mesma regra de [`02-dominio-hibrido.md`](02-dominio-hibrido.md), identidade não pertence ao agregado. `ActorUserId`/`SessionId` entram no momento de materializar o registro, via `ICurrentUser` resolvido no mesmo escopo da requisição, nunca carregado como campo do evento de domínio em si.

## Ação automatizada, sem ator humano

Identidade técnica vs. identidade do usuário final já é regra estabelecida em [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md). O que é específico deste documento: quando a operação crítica é disparada por um processo automatizado (job agendado, worker), esse mesmo identificador técnico do processo é o que vira o `ActorUserId` (ou campo equivalente) do registro de auditoria — nunca um `ActorUserId` fabricado como se um usuário real tivesse agido.

## Motivo obrigatório para ações administrativas sensíveis

Para as operações mais sensíveis (override administrativo, exceção a uma regra padrão), o `Command` correspondente exige um campo de motivo não vazio, validação sintática comum (ver [`16-validacao-sintatica-vs-invariante.md`](16-validacao-sintatica-vs-invariante.md)), não uma decisão nova.

## O que não é responsabilidade deste mecanismo

Log técnico ([`15-observabilidade.md`](15-observabilidade.md)) continua sendo log técnico: auditoria não o substitui. São registros complementares, com propósito diferente. Um serve diagnóstico operacional, o outro serve prestação de contas de negócio. Ambos são correlacionáveis pelo mesmo identificador de correlação/sessão, mas gravados e consultados de formas diferentes.

Os dois mecanismos também são independentes em falha: se o log técnico falhar (ex.: sink de log indisponível), a gravação do registro de auditoria não é afetada, pois ela acontece na mesma transação de negócio, via o interceptor de `SaveChanges` descrito acima, sem depender do log técnico ter funcionado. O inverso também vale: uma falha ao gravar o registro de auditoria não impede o log técnico de registrar o que aconteceu. Nenhum dos dois é pré-requisito do outro.

## Veja também

- [`15-observabilidade.md`](15-observabilidade.md): distinção entre auditoria e log técnico
- [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md): por que auditoria não precisa de Outbox, diferente de notificação entre módulos/sistemas externos
- [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md): identidade técnica vs. humana, reaplicada aqui a ações automatizadas
- [`16-validacao-sintatica-vs-invariante.md`](16-validacao-sintatica-vs-invariante.md): motivo obrigatório como validação sintática
