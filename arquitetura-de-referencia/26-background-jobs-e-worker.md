# Background jobs e worker

> Este documento é o complemento operacional de [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md): lá está o **conceito** (quando usar Outbox + worker vs. dispatch in-process, e o critério de "só quando o gatilho se materializar"); aqui está a **operação** do worker em si — como ele é hospedado, como consome o Outbox de forma segura com múltiplas instâncias, como trata retry, poison message e jobs agendados, sob qual identidade roda, e como é observado. Não redecide quando um evento merece Outbox (isso é do doc 05) nem redefine o mecanismo de correlação entre processos (isso é do doc [`15-observabilidade.md`](15-observabilidade.md)). Reaproveita [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md) (adapters chamados a partir do worker), [`22-migracao-de-schema.md`](22-migracao-de-schema.md) e [`23-retencao-e-expurgo-de-dados.md`](23-retencao-e-expurgo-de-dados.md) (jobs de backfill e expurgo), [`21-auditoria.md`](21-auditoria.md) e [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md) (o ator de sistema tratado como já estabelecido).

## Hospedagem: worker é um deployable separado

**Regra direta: o worker é um processo (deployable) separado do processo web, não uma `BackgroundService` hospedada dentro da Api.** O doc [`15-observabilidade.md`](15-observabilidade.md) já parte disso ("o processo web e um worker separado são deployables distintos desde o primeiro módulo que precisar de Outbox"), e o doc 05 idem. O motivo é de fronteira de processo e de escala: o web escala pela carga de requisições HTTP, o worker escala pela profundidade da fila de Outbox — são dimensões independentes. Hospedar o consumo de Outbox dentro da Api acopla as duas e faz o worker competir pelo mesmo pool de threads que atende o usuário (mesma família de risco descrita em [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md) para chamada externa síncrona).

Dentro do processo worker, cada consumidor/job é um `IHostedService`, tipicamente via `BackgroundService`:

```csharp
public sealed class OutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    public OutboxDispatcher(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            try { await DrainBatchAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // falha do ciclo, não de um evento: loga e continua no próximo tick.
                // nunca deixa a exceção derrubar o BackgroundService inteiro.
            }
        }
    }
}
```

Como o worker cria seu próprio escopo de DI por lote/evento (via `IServiceScopeFactory`), ele reusa exatamente o mesmo Mediator, os mesmos handlers e os mesmos pipeline behaviors da Api: um evento de Outbox despacha um `Command`/notificação via `ISender`/`IPublisher` como qualquer outro fluxo. Não existe um "caminho paralelo" de handlers só do worker.

## Consumo do Outbox: polling vs. notificação

| Mecanismo | Quando usar | Custo |
| --- | --- | --- |
| Polling da tabela (`PeriodicTimer` + `SELECT ... FOR UPDATE`) | Default. Suficiente para volumes normais; um lag de poucos segundos é aceitável para efeito colateral assíncrono | Uma query recorrente ociosa quando não há evento; latência = intervalo de poll |
| Notificação (ex.: `LISTEN/NOTIFY` do PostgreSQL) | Só quando o lag do polling se torna um problema real medido, não presumido | Um canal de notificação a operar e monitorar; ainda precisa de polling como fallback para eventos gravados antes do worker subir |

**Regra direta: comece com polling; só introduza notificação quando o lag medido do polling for insuficiente para um fluxo concreto.** É o mesmo espírito de "só quando o gatilho se materializar" do doc 05: notificação é otimização, não default. Note que notificação **nunca substitui** o polling, apenas o complementa — um evento gravado enquanto o worker estava fora do ar só será visto pelo varrimento periódico.

### Semântica at-least-once e por que o consumidor é idempotente

**Regra direta: a entrega do Outbox é at-least-once; o mesmo evento pode ser processado mais de uma vez, e por isso todo consumidor precisa ser idempotente.** Isso não é uma falha do mecanismo — é uma consequência inevitável de não haver commit atômico entre "executar o efeito colateral" e "marcar o evento como processado". Se o worker despacha o handler com sucesso e cai antes de gravar a marcação de processado, o evento será redespachado no próximo ciclo.

Dedup por identidade do evento é o mecanismo padrão: cada `OutboxEvent` tem um `Id` estável; o consumidor registra os ids já processados (tabela de dedup, ou a própria coluna `ProcessedAt` na linha do Outbox) e ignora o reprocessamento de um id já concluído. Para o caso em que o próprio efeito colateral é uma escrita de negócio, a idempotência reaproveita o mecanismo já descrito em [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md) (o `IdempotencyBehavior` do pipeline), usando o `Id` do evento como chave de idempotência do `Command` despachado.

## Retry e backoff do consumidor

Uma falha ao processar um evento cai em uma de duas naturezas, e o tratamento é diferente para cada:

| Natureza | Exemplo | Tratamento |
| --- | --- | --- |
| Transitória | Sistema externo momentaneamente fora do ar, timeout, deadlock de banco, `ConcurrencyException` | Re-tenta, com backoff exponencial entre tentativas |
| Permanente / envenenada | Payload malformado, invariante de domínio que nunca vai passar, `Result.Failure` determinístico | Não adianta re-tentar; vai direto para dead-letter |

**Regra direta: retry só faz sentido para falha transitória; falha permanente não deve consumir tentativas.** Distinguir as duas nem sempre é trivial no ponto da falha — quando em dúvida, trate como transitória, mas com um teto de tentativas (a seção seguinte). O backoff é exponencial para não martelar um sistema externo que já está em dificuldade (mesmo racional do circuit breaker do adapter em [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md)).

O contador de tentativas e o horário da próxima elegibilidade ficam na própria linha do Outbox (`AttemptCount`, `NextAttemptAt`), não em memória do worker: memória não sobrevive a um restart, e o backoff precisa sobreviver.

## Poison message e dead-letter

**Regra direta: após N tentativas, o evento é movido para dead-letter (ou marcado como falho) e gera alerta; nunca fica em loop infinito bloqueando a fila.** Um evento que falha sempre e é reprocessado eternamente no topo da fila trava todo o resto atrás dele — a fila para de progredir por causa de um único registro. O teto de tentativas é a proteção contra isso.

Dead-letter aqui não precisa de infraestrutura de fila dedicada: é uma coluna de status (`Failed`) ou uma tabela `OutboxDeadLetter` para onde a linha é movida, preservando payload, contagem de tentativas e a última exceção. O ponto obrigatório é o **alerta**: uma linha em dead-letter é uma intervenção humana pendente, não um estado silencioso. O reprocessamento posterior de um dead-letter (depois de corrigida a causa) é uma operação administrativa consciente, não automática.

## Ordering e concorrência entre múltiplas instâncias

Escalar o worker significa rodar mais de uma instância consumindo a **mesma** tabela de Outbox. Sem coordenação, duas instâncias leem a mesma linha e processam o mesmo evento em paralelo — a idempotência do consumidor evita o efeito duplicado, mas ainda desperdiça trabalho e pode causar corrida no efeito colateral externo.

**Regra direta: cada linha do Outbox é reivindicada (claim) por exatamente uma instância antes de ser processada, via claim atômico no próprio banco.** Duas formas equivalentes, ambas usando a transação do banco como árbitro:

- **`SELECT ... FOR UPDATE SKIP LOCKED`** (ou `UPDATE ... RETURNING` num único statement): a instância trava as linhas do lote e as demais instâncias pulam as linhas já travadas em vez de bloquear. É o mecanismo preferido quando o banco o suporta (PostgreSQL, entre outros).
- **Coluna de lease com timeout** (`LockedBy`, `LockedUntil`): a instância marca a linha como sua até um horário-limite; se o worker cair sem concluir, o lease expira e outra instância pode reivindicar. Necessário quando o banco não oferece `SKIP LOCKED`, ao custo de uma janela igual ao timeout do lease antes de um evento órfão voltar a ser elegível.

Sobre **ordering**: com múltiplas instâncias, a ordem global de processamento **não** é garantida. Se um fluxo exige ordem (ex.: eventos do mesmo agregado processados em sequência), a ordenação precisa ser explícita — particionar por uma chave (`AggregateId`) de modo que eventos da mesma chave nunca sejam processados em paralelo. Não presuma ordem que não foi projetada; a maioria dos efeitos colaterais assíncronos tolera reordenação, e exigir ordem global elimina o ganho de escalar instâncias.

```csharp
private async Task DrainBatchAsync(CancellationToken ct)
{
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var accessor = scope.ServiceProvider.GetRequiredService<ICurrentUserAccessor>();
    var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

    // o worker roda sob identidade de sistema por todo o lote (ver seção adiante)
    using var _ = accessor.BeginSystemIdentity("OutboxDispatcher");

    // claim atômico: FOR UPDATE SKIP LOCKED garante que outra instância
    // não pegue as mesmas linhas. A transação delimita o claim.
    await using var tx = await db.Database.BeginTransactionAsync(ct);

    var batch = await db.OutboxEvents
        .FromSqlRaw(@"SELECT * FROM outbox_events
                      WHERE processed_at IS NULL AND next_attempt_at <= now()
                      ORDER BY occurred_at
                      LIMIT 20
                      FOR UPDATE SKIP LOCKED")
        .ToListAsync(ct);

    foreach (var evt in batch)
    {
        try
        {
            // restaura a correlação persistida junto com o evento (ver doc 15)
            using var activity = StartActivityFrom(evt.CorrelationId);

            var notification = OutboxSerializer.Deserialize(evt);
            await publisher.Publish(notification, ct);

            evt.MarkProcessed(); // grava processed_at
        }
        catch (Exception ex) when (IsTransient(ex))
        {
            evt.ScheduleRetry(BackoffFor(evt.AttemptCount)); // incrementa e agenda
            if (evt.AttemptCount >= MaxAttempts)
                evt.MoveToDeadLetter(ex); // alerta é disparado a partir daqui
        }
        catch (Exception ex)
        {
            evt.MoveToDeadLetter(ex); // falha permanente: nem consome tentativas
        }
    }

    await db.SaveChangesAsync(ct);
    await tx.CommitAsync(ct);
}
```

## Jobs agendados (cron / recorrentes)

Além de consumir o Outbox, o worker hospeda jobs recorrentes por tempo, não por evento: expurgo/anonimização (ver [`23-retencao-e-expurgo-de-dados.md`](23-retencao-e-expurgo-de-dados.md)) e backfill de dados durante uma migração faseada (ver [`22-migracao-de-schema.md`](22-migracao-de-schema.md)).

| Mecanismo | Quando usar | Custo |
| --- | --- | --- |
| `PeriodicTimer` num `BackgroundService` | Job simples, uma instância de worker, sem persistência de estado de agendamento nem UI | Nenhuma dependência nova; sem coordenação entre instâncias; sem histórico de execução |
| Quartz.NET | Precisa de cron real, jobs persistidos, e coordenação entre múltiplas instâncias (cluster) para não executar o mesmo job duas vezes | Uma dependência e tabelas de agendamento a operar |
| Hangfire | Além do acima, precisa de dashboard/UI de jobs e enfileiramento ad hoc | Dependência maior, storage e dashboard a operar/proteger |

**Regra direta: use o mínimo suficiente e escale por necessidade real — `PeriodicTimer` para o caso simples, um agendador dedicado (Quartz.NET, ou Hangfire se precisar de dashboard) só quando cron real ou coordenação entre instâncias se materializar.** Mesmo espírito de "só quando o gatilho se materializar" do doc 05: não adote Hangfire "porque um dia pode ter muitos jobs".

**Idempotência do job é obrigatória, do mesmo modo que a do consumidor de Outbox:** rodar o job duas vezes (por retry, por duas instâncias sem coordenação, ou por um reinício no meio da execução) não pode duplicar o efeito. Um job de expurgo é naturalmente idempotente (apagar o que já não existe é no-op); um job de backfill precisa ser escrito para retomar de onde parou (`WHERE coluna_nova IS NULL`), nunca reprocessar cegamente o que já migrou. Se o agendador escolhido não garante execução única entre instâncias, o próprio job garante — via claim/lease igual ao do Outbox, ou via a chave natural do que ele processa.

## Identidade do ator no worker

**Regra direta: todo processamento no worker roda sob identidade de sistema, não sob a identidade de um usuário.** O worker não tem uma requisição HTTP autenticada por trás; ele age em nome do próprio sistema. O ator é estabelecido via `ICurrentUserAccessor.BeginSystemIdentity(processName)`, que torna `ICurrentUser.IsSystemActor` = `true` durante o escopo:

```csharp
using var _ = accessor.BeginSystemIdentity("OutboxDispatcher");
// dentro deste escopo, ICurrentUser.IsSystemActor == true
```

Isso não é um caso especial que os cross-cutting concerns precisam aprender a tolerar: [`21-auditoria.md`](21-auditoria.md) e [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md) já tratam o ator de sistema como estabelecido. A auditoria registra o `ActorUserId` de sistema (ou marca a origem como automatizada) exatamente como registraria um usuário; a autorização por recurso (`AuthorizationBehavior`/`IRequiresAuthorization`) enxerga `IsSystemActor` e decide conforme sua própria regra. O worker não desliga o pipeline nem contorna esses behaviors — ele só entra no pipeline com um ator diferente.

Um detalhe de origem de correlação: quando o evento de Outbox foi originado por uma ação de usuário real, o `SessionId`/correlação daquele usuário viaja **no payload persistido do evento** (próxima seção), não na identidade do worker. A identidade do worker é de sistema; a correlação de negócio que originou o evento é um dado carregado, não a identidade sob a qual o worker roda.

## Observabilidade do worker

O worker é uma fronteira de processo, então tudo do doc [`15-observabilidade.md`](15-observabilidade.md) sobre correlação entre processos se aplica aqui, sem redefinição:

- **Correlação propagada, não reinventada.** O identificador de correlação nativo do processo web não atravessa a fronteira sozinho: ele é **persistido junto com o evento no Outbox** (`CorrelationId` na linha) no momento da gravação, e o worker o **restaura** ao iniciar o processamento (o `StartActivityFrom(evt.CorrelationId)` do exemplo acima), religando o log/trace do worker ao da requisição HTTP que originou o evento. Isso é OpenTelemetry aplicado à fronteira Api → worker, exatamente o gatilho sistêmico que o doc 15 usa para exigir tracing distribuído.
- **Métricas de fila.** O worker expõe, no mínimo: **lag** (idade do evento não processado mais antigo — o sinal mais direto de que o worker não dá conta da carga), **backlog** (contagem de eventos não entregues, `processed_at IS NULL`), e **dead-letter** (contagem em `Failed`/`OutboxDeadLetter`, que deve disparar alerta a qualquer valor > 0). Contagem de dead-letter crescente é a métrica que mais cedo revela um consumidor quebrado; lag crescente revela subdimensionamento de instâncias.

## Nota de aplicação

O nome exato das colunas de controle (`processed_at`, `attempt_count`, `next_attempt_at`, `locked_until`), o valor de `MaxAttempts`, o intervalo de poll e a curva de backoff são parâmetros de cada sistema, não decisões desta arquitetura de referência — do mesmo modo que os prazos do doc 23 são placeholders. A disponibilidade de `SELECT ... FOR UPDATE SKIP LOCKED` depende do banco concreto: onde não existir, use a coluna de lease. E um sistema que ainda roda em processo único, sem nenhum fluxo que cruze a fronteira do processo (critério do doc 05), não tem worker algum — este documento só passa a valer quando o primeiro gatilho de Outbox ou de job agendado se materializa de fato.

**Nível de confiança:** as regras de deployable separado, at-least-once/idempotência, claim de linha, dead-letter com alerta e identidade de sistema são síntese direta dos docs 05, 07, 15, 21 e 12 já estabelecidos, com alta confiança. As escolhas concretas de biblioteca de agendamento (Quartz.NET vs. Hangfire vs. `PeriodicTimer`) e de mecanismo de claim são recomendações de trade-off, não obrigações — cada sistema decide pelo mínimo suficiente à sua carga real.

## Veja também

- [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md): o conceito de Outbox + worker e o critério de quando ele se justifica; este documento é o complemento operacional dele
- [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md): adapters de sistema externo/legado tipicamente chamados a partir do worker, e o mesmo racional de resiliência/backoff
- [`15-observabilidade.md`](15-observabilidade.md): correlação entre processos, persistida junto ao evento no Outbox, e o gatilho de OpenTelemetry para fronteira de processo
- [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md): `IdempotencyBehavior` reaproveitado pelo consumidor, e `ConcurrencyException` como falha transitória
- [`21-auditoria.md`](21-auditoria.md): registro do ator de sistema, tratado como já estabelecido
- [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md): `AuthorizationBehavior` enxergando `IsSystemActor`, tratado como já estabelecido
- [`22-migracao-de-schema.md`](22-migracao-de-schema.md): job de backfill idempotente durante migração faseada expand/contract
- [`23-retencao-e-expurgo-de-dados.md`](23-retencao-e-expurgo-de-dados.md): job agendado de expurgo/anonimização, naturalmente idempotente
- [`28-push-em-tempo-real.md`](28-push-em-tempo-real.md): como o worker avisa o cliente da conclusão, via `IUserNotifier` e backplane
