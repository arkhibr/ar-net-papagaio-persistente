# Processamento assíncrono e eventos de integração

## Dois mecanismos, não um

É comum, ao desenhar processamento assíncrono num monólito modular, tratar Outbox + worker como o único mecanismo para toda comunicação que não é síncrona. Isso mistura dois problemas de custo muito diferente:

| Mecanismo | Quando usar | Custo |
| --- | --- | --- |
| Dispatch in-process (pós-commit, `INotificationHandler` → novo `Command` via `ISender`) | Orquestração **entre módulos do próprio monólito** (o efeito colateral não sai do processo) | Nenhum componente novo; garantia "melhor esforço, log e segue" |
| Outbox + worker | Efeito colateral que **sai do processo**: chamar um sistema externo, disparar notificação, ou qualquer caso em que a entrega precisa sobreviver a uma queda do processo entre commit e dispatch | Tabela extra, processo de publicação, monitoramento de eventos não entregues |

**Regra direta: Outbox é para cruzar a fronteira do processo (sistema externo, ou garantia de entrega que o dispatch in-process não oferece). Orquestração entre módulos do mesmo monólito usa dispatch in-process**, salvo quando o critério abaixo apontar para Outbox.

## Dispatch in-process

Este é o padrão default entre módulos do mesmo monólito: o `DbContext.SaveChangesAsync` publica os eventos de domínio acumulados no agregado, depois do commit, via `IPublisher`. Um `INotificationHandler` no módulo que reage despacha um novo `Command` do `Contracts` do módulo dono daquele fluxo (ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)). Falha na publicação é logada e engolida, não derruba a operação de negócio já persistida.

## Outbox + worker

Esse mecanismo é reservado para quando o efeito colateral sai do processo, cobrindo dois casos:

1. Entrega eventual garantida mesmo com queda do processo entre commit e dispatch.
2. Atomicidade real necessária entre o efeito colateral (chamar um sistema externo) e a transação de negócio que o originou.

```text
Evento de negócio confirmado
        │
        ▼
transação
├── atualiza o agregado
└── grava OutboxEvent
        │
        ▼
Worker
        │
        ├── sistema externo (integração legada, notificação — ver 07-integracao-legado-acl.md)
        └── projeção de read model entre módulos (ver 04-comunicacao-entre-modulos.md)
```

**É o mesmo componente de worker nos dois ramos, não dois workers separados.** Um único worker consome a tabela de Outbox e despacha para o handler correto por tipo de evento: o que muda é o handler registrado para cada tipo, não o processo/worker em si. Não há um worker dedicado a "eventos que saem para sistema externo" e outro a "eventos que projetam read model entre módulos": essa distinção é de handler, não de infraestrutura de consumo.

## Quando um evento entre módulos ainda justifica Outbox

O dispatch in-process é o default, não uma regra sem exceção:

- Mais de um módulo precisa reagir ao mesmo evento (fan-out real, não um único handler).
- A garantia "melhor esforço, log e segue" deixa de ser aceitável para aquele fluxo específico.
- O consumidor do evento é, na prática, um read model de leitura entre módulos; nesse caso o Outbox não é opcional, é o próprio mecanismo de projeção (ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)).

O critério é sobre quantidade de handlers **já existente e necessária hoje**, não sobre especulação de quantos módulos podem vir a reagir no futuro. "Mais de um módulo precisa reagir" significa que mais de um handler já existe ou já está sendo implementado nesta mudança, nunca uma antecipação de um handler que ainda não existe.

## Só introduza Outbox quando um gatilho se materializar de fato

Nunca antecipadamente, "porque pode precisar no futuro". Cada tabela de Outbox e cada consumidor no worker é operação e monitoramento adicional; só se paga quando o gatilho é real, não hipotético.

## Veja também

- [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md): dispatch in-process como mecanismo de escrita entre módulos, Outbox como mecanismo de projeção de read model
- [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md): adapters tipicamente chamados a partir do worker
