# Transação, Unit of Work e disparo de eventos de domínio

> Consolida o que estava implícito e espalhado: [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md) cita `UnitOfWork` como quem traduz conflito de concorrência, [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md) cita dispatch pós-commit e gravação no Outbox, [`21-auditoria.md`](21-auditoria.md) exige gravação na mesma transação — mas nenhum documento dizia **quem abre a transação, quem chama `SaveChanges`, quando, e como os eventos de domínio se posicionam em relação ao commit**. Este documento é essa fronteira. Também reconcilia uma divergência real entre exemplos: [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md) mostra um handler sem `SaveChanges` visível; [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md) mostra `SalvarAsync`. As duas formas convergem para a regra abaixo.

## Uma transação por `Command`, um commit só, no `UnitOfWorkBehavior`

**Regra direta: cada requisição de `Command` tem exatamente uma transação e um `SaveChanges`, chamados uma única vez pelo `UnitOfWorkBehavior`, nunca pelo handler.** O handler expressa intenção — carrega o agregado, invoca o método de negócio, encena a mudança no repositório — mas não commita. O commit é responsabilidade central de um único ponto.

Isso resolve a divergência dos exemplos: `AdicionarAsync`/`SalvarAsync` de um repositório **encenam** a mudança (`Add`/`Update` no change tracker do EF Core), não gravam. A gravação é o `SaveChangesAsync` único do `UnitOfWorkBehavior`, depois que o handler retorna com sucesso. Um handler nunca chama `SaveChanges`; se um handler precisa "salvar duas vezes", isso é sinal de que são dois casos de uso, não um.

```csharp
public interface IUnitOfWork
{
    // Único ponto da solução que chama SaveChanges e nomeia DbUpdateConcurrencyException.
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
```

## Onde o `UnitOfWorkBehavior` se posiciona em relação à ordem fixa

A ordem fixa de pipeline behaviors (`Logging → Validation → Authorization → Idempotency → Caching → Handler`, ver [`03-commands-e-queries.md`](03-commands-e-queries.md)) não muda. O `UnitOfWorkBehavior` é o **envelope transacional mais interno**, executando imediatamente ao redor do handler: os behaviors da ordem fixa são checagens de corte (rejeitar cedo, barato) que rodam **antes** de qualquer transação abrir; a transação só abre quando a requisição já passou por validação, autorização e idempotência e vai de fato executar o handler.

```text
Logging
  └─ Validation
       └─ Authorization
            └─ Idempotency ──────────────┐ (reserva a chave; ver "Atomicidade")
                 └─ Caching              │
                      └─ UnitOfWork  ◄───┘ abre transação, chama o handler,
                           └─ Handler       commita UMA vez se o Result for sucesso
```

**Regra direta: a transação commita só quando o handler retorna um `Result` de sucesso. `Result.Failure`, `Result.NotFound` ou qualquer exceção fazem rollback (nenhum `SaveChanges` acontece).** Uma falha de negócio determinística ([`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md)) não deixa efeito colateral parcial no banco.

```csharp
public sealed class UnitOfWorkBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : ITransactionalCommand
    where TResponse : IResult   // interface-base de Result<T>/Result, para o behavior inspecionar sucesso
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDomainEventDispatcher _dispatcher;

    public UnitOfWorkBehavior(IUnitOfWork unitOfWork, IDomainEventDispatcher dispatcher)
    {
        _unitOfWork = unitOfWork;
        _dispatcher = dispatcher;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(message, cancellationToken);
        if (response.IsFailure)
            return response; // rollback implícito: SaveChanges nunca é chamado

        // Um único commit: mudança de negócio + registro de auditoria (interceptor, ver 21)
        // + linhas de Outbox (integração, ver 05) + registro de idempotência, atômicos.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Só depois do commit: eventos de domínio in-process (best effort, ver 05).
        await _dispatcher.DispatchPostCommitAsync(cancellationToken);
        return response;
    }
}
```

Query nunca passa por este behavior: leitura não tem transação de escrita nem commit (ver [`27-leitura-e-query-side.md`](27-leitura-e-query-side.md), `AsNoTracking`). O marcador `ITransactionalCommand` (ou a própria natureza `IRequest` de `Command` vs. `Query`) decide se o behavior atua, mesma mecânica opt-in dos demais behaviors.

## O que entra na mesma transação (atomicidade)

**Regra direta: tudo que precisa ser verdade junto com a operação de negócio grava no mesmo `SaveChanges`, nunca em uma segunda transação.** São quatro coisas, e cada uma tem seu documento dono:

| O que | Como entra na transação | Documento dono |
| --- | --- | --- |
| Mudança do agregado | Change tracker do EF Core, encenado pelo repositório | [`02-dominio-hibrido.md`](02-dominio-hibrido.md) |
| Registro de auditoria | `ISaveChangesInterceptor`, materializado antes do commit | [`21-auditoria.md`](21-auditoria.md) |
| Linhas de Outbox (evento de integração) | Encenadas na transação; publicadas depois pelo worker | [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md) |
| Registro de idempotência (marcação de concluído) | Encenado na transação; ver "Idempotência" abaixo | [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md) |

O ganho de centralizar o commit num ponto só é exatamente esse: as quatro escritas compartilham uma transação sem que o handler precise coordená-las manualmente. Perder o registro de auditoria de uma operação que commitou, ou marcar idempotência de uma operação que deu rollback, deixa de ser possível por construção.

## Eventos de domínio: coletados no agregado, publicados pós-commit

**Regra direta: o agregado acumula eventos de domínio internamente; eles são publicados in-process depois do commit, nunca durante o handler.** Publicar antes do commit dispararia reação a um fato que ainda pode dar rollback.

O fluxo:

1. O método de negócio do agregado registra um evento (`AddDomainEvent`) — o agregado não sabe quem vai reagir nem quando.
2. O handler encena a mudança; o `UnitOfWorkBehavior` commita.
3. Após o commit, o `IDomainEventDispatcher` coleta os eventos dos agregados rastreados e os publica via `IPublisher.Publish` (Mediator). Um `INotificationHandler` no módulo que reage despacha um novo `Command` (ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)).
4. Falha na publicação pós-commit é logada e engolida ("melhor esforço, log e segue", ver [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md)); não desfaz a operação já persistida.

**Evento de domínio (in-process, dentro do processo) vs. evento de integração (Outbox, cruza processo)** é a distinção de [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md) e [`08-convencao-de-nomenclatura.md`](08-convencao-de-nomenclatura.md). O evento de integração **não** é publicado pós-commit em memória: ele vira linha de Outbox **dentro** da transação (item da tabela acima), e o worker o publica depois (ver [`26-background-jobs-e-worker.md`](26-background-jobs-e-worker.md)). Só o evento de domínio in-process usa o dispatch pós-commit descrito aqui.

```csharp
// Domain — o agregado só descreve o que mudou; não sabe de transação nem de quem reage.
internal sealed class Pedido
{
    private readonly List<IDomainEvent> _eventos = new();
    public IReadOnlyList<IDomainEvent> EventosDeDominio => _eventos;

    public void Confirmar(DateTimeOffset em) // tempo chega resolvido, ver 13
    {
        if (Situacao != SituacaoPedido.Rascunho)
            throw new DomainException("Só um pedido em rascunho pode ser confirmado.");

        Situacao = SituacaoPedido.Confirmado;
        _eventos.Add(new PedidoConfirmadoEvent(Id, em));
    }
}
```

## Concorrência otimista: `RowVersion` traduzido num só lugar

**Regra direta: `UnitOfWork.SaveChangesAsync` é a única classe da solução que captura `DbUpdateConcurrencyException` e a traduz para uma exceção própria da Application (`ConcurrencyException`, 409), antes de propagar.** Nenhum handler, repositório ou controller menciona `DbUpdateConcurrencyException` pelo nome. `RowVersion` é shadow property do EF Core no agregado (ver [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md)).

```csharp
public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
{
    try
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateConcurrencyException ex)
    {
        throw new ConcurrencyException("O recurso foi alterado por outra operação.", ex);
    }
}
```

`ConcurrencyException` é falha transitória, então é **exceção não capturada** que o `GlobalExceptionHandler` traduz para 409 — nunca um `Result.Failure`, porque uma nova tentativa pode ter sucesso e não deveria ser cacheada pela idempotência como conclusão definitiva (o racional está em [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md)).

## Idempotência participa da mesma transação

O `IdempotencyBehavior` roda **antes** do `UnitOfWorkBehavior` na ordem fixa, mas o registro de idempotência precisa commitar **junto** com a operação de negócio, senão haveria uma janela em que a operação commitou mas a marcação de "concluída" não (ou o inverso). A conciliação:

- Na **primeira** chegada de uma `Idempotency-Key`, o `IdempotencyBehavior` reserva a chave (insere um registro "em andamento", garantido por índice único) e deixa a requisição seguir. Uma segunda chegada concorrente com a mesma chave encontra a reserva e recebe `OperationInProgressException` (409, ver [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md)).
- A **marcação de concluída** (e a resposta a devolver em reenvios futuros) é encenada no mesmo `DbContext` e commitada pelo mesmo `SaveChanges` do `UnitOfWorkBehavior`. Por isso o `IdempotencyBehavior` e o `UnitOfWorkBehavior` compartilham o mesmo `DbContext` com lifetime `Scoped` da requisição: é o que permite as duas escritas serem uma transação só.
- Reenvio depois de concluída: o `IdempotencyBehavior` encontra o registro concluído e devolve a resposta armazenada sem chamar o handler — a transação nem chega a abrir.

**Consequência aceita:** há um acoplamento entre idempotência e a fronteira transacional (ambas dependem do mesmo `DbContext` scoped). Isso é deliberado — é o preço da atomicidade entre "a operação aconteceu" e "a operação está marcada como concluída". A alternativa (duas transações) reintroduziria exatamente a janela de inconsistência que a idempotência existe para fechar.

## Transação e múltiplos agregados

Uma transação deveria mudar **um** agregado por vez, por padrão: essa é a unidade de consistência (ver [`02-dominio-hibrido.md`](02-dominio-hibrido.md)). Quando um fluxo precisa afetar dois agregados, o caminho normal não é uma transação que abarca os dois — é o primeiro agregado commitar e emitir um evento de domínio que dispara um `Command` sobre o segundo, com consistência eventual (ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)). Abarcar dois agregados numa transação só se justifica dentro do mesmo módulo, quando os dois genuinamente precisam ser consistentes no mesmo instante — e, se precisam, vale reavaliar se não são o mesmo agregado. Entre módulos, nunca: cada módulo commita a própria transação.

## Nível de confiança

O posicionamento do `UnitOfWorkBehavior` como envelope interno e o commit único condicionado ao sucesso do `Result` são síntese própria a partir das restrições já presentes na referência (ordem fixa de pipeline, atomicidade exigida por auditoria/Outbox/idempotência). A alternativa de `SaveChanges` explícito no handler é viável e alguns times a preferem por ser mais literal; ela foi preterida aqui porque espalha a coordenação transacional (auditoria, Outbox, idempotência, eventos pós-commit) por cada handler, em vez de centralizá-la. Vale validação por quem opera os sistemas reais adotando o padrão.

## Nota de aplicação

Um sistema concreto pode divergir conscientemente, por exemplo:

- Chamar `SaveChanges` explicitamente no handler em vez de usar o `UnitOfWorkBehavior`, aceitando repetir a coordenação transacional — por exemplo, quando o time prefere a fronteira transacional visível na leitura do próprio handler à centralização no behavior.
- Usar transação explícita do banco (`BeginTransactionAsync`) em vez do commit implícito do `SaveChanges`, quando um fluxo específico precisa de controle transacional mais fino (ex.: savepoints).

## Veja também

- [`03-commands-e-queries.md`](03-commands-e-queries.md): ordem fixa de pipeline; o `UnitOfWorkBehavior` como envelope interno
- [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md): dispatch pós-commit in-process vs. Outbox dentro da transação
- [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md): `RowVersion`, `ConcurrencyException`, `OperationInProgressException`, `Result.Failure` vs. exceção
- [`21-auditoria.md`](21-auditoria.md): registro de auditoria via interceptor, na mesma transação
- [`26-background-jobs-e-worker.md`](26-background-jobs-e-worker.md): o worker que publica as linhas de Outbox depois do commit
- [`27-leitura-e-query-side.md`](27-leitura-e-query-side.md): por que `Query` não passa por este behavior
