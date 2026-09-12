# Observabilidade e logging estruturado

## O que o `LoggingBehavior` cobre

Todo command/query que passa pelo Mediator já loga o nome da mensagem e o tempo de execução, sem código adicional por handler (ver [`03-commands-e-queries.md`](03-commands-e-queries.md)). Isso não cobre o log de negócio específico de um evento, que continua no `INotificationHandler` daquele evento, com o nível certo de detalhe para aquele caso.

## Correlação nativa da plataforma

Nunca invente um `RequestId` próprio nem o passe manualmente por assinatura de método. Use o identificador de correlação que o framework já gera por requisição (`HttpContext.TraceIdentifier`, `Activity.Current` quando tracing está habilitado). Torne-o presente em todo log da requisição via enriquecimento do provedor de log, uma única vez, no início do pipeline HTTP.

## Correlação entre processos

Um monólito modular normalmente já é mais de um processo: o processo web e um worker separado para processamento assíncrono são deployables distintos desde o primeiro módulo que precisar de Outbox (ver [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md)), e qualquer chamada a um sistema externo/legado (ver [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md)) já é uma fronteira de processo. Um identificador de correlação nativo do framework web não atravessa essas fronteiras sozinho: precisa ser propagado explicitamente, persistido junto com o evento no Outbox, incluído como header/campo de contexto na chamada ao sistema externo.

## Formato da chamada de log

Toda chamada de log usa placeholder nomeado, nunca interpolação de string: só a primeira forma permite consultar/filtrar logs por campo estruturado num backend de log.

Para os caminhos quentes, prefira o gerador `[LoggerMessage]` (source-generated logging): ele produz o log estruturado em tempo de compilação, sem boxing nem parsing de template em runtime, mantendo o mesmo placeholder nomeado. É otimização, não obrigação — o `ILogger` com placeholder nomeado direto continua correto para o caso geral.

## Nível de log por natureza do evento

| Nível | Quando usar |
| --- | --- |
| `Information` | Evento de ciclo de vida de negócio, esperado |
| `Warning` | Degradação com fallback automático, sem impacto no resultado da operação (ex.: circuit breaker semiaberto num adapter de integração externa) |
| `Error` | Falha não prevista, que efetivamente impede a operação |

**Nunca** logue como `Error` uma falha que já tem caminho de negócio esperado, pois isso transforma ruído operacional normal em alerta de produção.

## Campo sensível no log

Nunca logue um campo sensível: o mesmo critério de visibilidade de campo de [`14-filtro-de-dados.md`](14-filtro-de-dados.md) se aplica ao log: um campo que não deveria aparecer no DTO de um público sem permissão também não deveria aparecer em texto livre num log.

## Quando adotar OpenTelemetry

**Regra direta:** adote rastreamento distribuído no momento em que existir mais de um processo cuja correlação importa para diagnosticar um problema: worker separado, integração síncrona com sistema externo, mais de um módulo em deployables distintos, ou um módulo promovido/extraído do monólito para um deployable separado (ver critério de promoção em [`01-estrutura-de-projetos-monolito-modular.md`](01-estrutura-de-projetos-monolito-modular.md)) — o próprio ato de extração já cria a fronteira de processo que exige correlação explícita. Num sistema de camada única, sem worker nem integração externa, o identificador de correlação nativo do framework mais logging estruturado já cobrem o essencial, sem a complexidade adicional de um coletor/backend de tracing.

**O gatilho acima é sistêmico, não temporal:** avalie se ele já está presente hoje, não se "presumivelmente vai aparecer depois". Um monólito modular que já nasce com worker separado ou integração síncrona com sistema legado já atende ao critério desde a primeira versão, e adiar OpenTelemetry nesse caso não é o mesmo adiamento razoável de um sistema de processo único, onde seria aceitável esperar até existir mais de um processo.

## .NET Aspire (opcional)

Quando o sistema já é multi-processo (web + worker separado, mais store de sessão/cache, mais banco — ver [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md), [`24-cache.md`](24-cache.md), [`26-background-jobs-e-worker.md`](26-background-jobs-e-worker.md)), o **.NET Aspire** é uma opção para orquestrar esses processos em desenvolvimento e padronizar a exportação de telemetria (OpenTelemetry já integrado). Não é requisito desta referência nem substitui as regras de correlação acima; é ferramenta de conveniência para o exato cenário multi-processo que já dispara o critério de OpenTelemetry. Adote se o ganho de orquestração local se pagar; ignore num sistema de processo único.

## Veja também

- [`03-commands-e-queries.md`](03-commands-e-queries.md): onde o `LoggingBehavior` se encaixa
- [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md): fronteira de processo entre Api e worker
- [`07-integracao-legado-acl.md`](07-integracao-legado-acl.md): fronteira de processo em chamadas a sistemas externos
- [`14-filtro-de-dados.md`](14-filtro-de-dados.md): critério de visibilidade de campo, reaplicado ao que não deveria ir para o log
