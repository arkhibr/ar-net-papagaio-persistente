# Push em tempo real ao cliente (SignalR/SSE)

> Fecha um laço que os demais documentos deixavam em aberto: [`17-bff-angular-e-comunicacao.md`](17-bff-angular-e-comunicacao.md) descreve Angular ↔ BFF só em request/response, e [`26-background-jobs-e-worker.md`](26-background-jobs-e-worker.md) processa fora da requisição HTTP — mas nenhum documento dizia **como o cliente fica sabendo** que uma operação assíncrona terminou, ou como recebe um evento ao vivo. Este documento cobre o canal de push do servidor para o cliente. Complementa [`17`](17-bff-angular-e-comunicacao.md) (mesma origem, mesma sessão), [`25`](25-transacao-e-unit-of-work.md) e [`26`](26-background-jobs-e-worker.md) (de onde o gatilho do push vem), [`11`](11-autenticacao-e-sessao.md) (autenticação da conexão) e [`14`](14-filtro-de-dados.md) (o dado que pode ou não ir numa mensagem).

## Push é UX/transporte, nunca fonte de verdade nem autorização

**Regra direta: uma mensagem enviada por push é uma dica ("algo mudou", "sua operação terminou"), não a fonte autoritativa do dado.** O cliente reage à mensagem re-buscando o estado pelo caminho normal de leitura (uma `Query`, ver [`27-leitura-e-query-side.md`](27-leitura-e-query-side.md)), nunca tratando o payload do push como verdade final nem como concessão de acesso. É o mesmo princípio de [`17-bff-angular-e-comunicacao.md`](17-bff-angular-e-comunicacao.md): o frontend usa o que o backend manda para UX, a autorização e o estado real vivem no backend.

Consequência direta para o conteúdo da mensagem: **a mensagem carrega o mínimo para o cliente saber o que re-buscar** (um id, um tipo de evento, um status), não um dump de dado de negócio. Fazer o cliente re-buscar via `Query` mantém o filtro de dado (por linha e por campo, [`14-filtro-de-dados.md`](14-filtro-de-dados.md)) num lugar só — o caminho de leitura já existente —, em vez de reimplementar redação de campo dentro do canal de push, onde seria fácil vazar por engano.

## Default é request/response; push só quando o gatilho aparece

**Regra direta: push não é o transporte default; é adotado quando há necessidade real.** Uma conexão persistente por cliente tem custo (estado de conexão entre réplicas, backplane, reconexão). Adote push quando um destes for verdade, não "porque é moderno":

- Notificar conclusão de uma operação assíncrona que rodou no worker ([`26-background-jobs-e-worker.md`](26-background-jobs-e-worker.md)) — o usuário disparou e o efeito acontece fora da resposta HTTP.
- Atualização ao vivo de uma tela (dashboard, painel colaborativo, contadores).
- Progresso de um job longo.

Para o caso "só preciso saber se meu comando terminou", às vezes **polling de um endpoint de status é suficiente e mais simples** — não introduza um hub de tempo real para isso. Mesmo espírito de "só quando o gatilho se materializar" de [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md) e [`20-feature-flags.md`](20-feature-flags.md).

## Entrega é best-effort; o estado autoritativo sempre existe sem o push

**Regra direta: push é entrega de melhor esforço, nunca o único canal de algo que o usuário não pode perder.** O cliente pode estar offline, com a conexão caída, ou entre reconexões. Portanto:

- Ao (re)conectar, o cliente **re-busca o estado atual** (a tela, a lista de notificações pendentes) por `Query`, em vez de depender de ter recebido cada push enquanto esteve fora.
- Push serve UX ao vivo, não entrega durável. Quando a mensagem **precisa** chegar (confirmação por e-mail, comprovante), isso é o concern de notificação durável, fora deste documento — push não substitui.

Isso espelha o "melhor esforço, loga e segue" do dispatch in-process de [`05-processamento-assincrono-e-eventos.md`](05-processamento-assincrono-e-eventos.md): o canal ao vivo pode falhar sem comprometer a operação de negócio, porque o estado autoritativo continua disponível pelo caminho de leitura normal.

## O módulo nunca depende de SignalR: abstração `IUserNotifier`

**Regra direta: `Domain`, `Application` e o worker nunca referenciam SignalR (nem `IHubContext`) diretamente. Eles dependem de uma abstração `IUserNotifier`, no `SharedKernel`; a implementação concreta (SignalR) mora na composição raiz.** É a mesma regra de [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md) ("`Domain`/`Application` nunca dependem de `HttpContext`") aplicada ao transporte de push: o mecanismo é infraestrutura, não domínio.

```csharp
// SharedKernel — o que qualquer módulo ou o worker precisa para notificar, sem saber como.
public interface IUserNotifier
{
    // Entrega uma mensagem a todas as conexões de um usuário específico. Best-effort.
    Task NotifyUserAsync(Guid userId, string evento, object carga, CancellationToken ct);
}
```

Quem transforma um evento de negócio em push é um handler que **reage** ao evento (mesma forma do handler reativo de [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)), morando na composição raiz ou num módulo de notificação dedicado — nunca dentro do módulo de domínio, que continua sem saber que push existe:

```csharp
// Api (composição) — reage a um evento pós-commit (ver 25) e empurra uma dica ao cliente.
internal sealed class NotificarConclusaoHandler
    : INotificationHandler<DomainEventNotification<PedidoConfirmadoEvent>>
{
    private readonly IUserNotifier _notifier;

    public async ValueTask Handle(
        DomainEventNotification<PedidoConfirmadoEvent> notification, CancellationToken ct)
    {
        var e = notification.DomainEvent;
        // carga mínima: id + tipo. O cliente re-busca o detalhe por Query (filtro de dado em um só lugar).
        await _notifier.NotifyUserAsync(e.DonoUserId, "pedido.confirmado", new { e.PedidoId }, ct);
    }
}
```

O worker de [`26-background-jobs-e-worker.md`](26-background-jobs-e-worker.md), ao concluir um processamento, chama o **mesmo** `IUserNotifier` — a implementação (via backplane, abaixo) roteia para a réplica que segura a conexão do usuário, sem o worker saber que réplica é essa.

## Backplane é obrigatório com mais de uma réplica ou worker separado

**Regra direta: a partir do momento em que existe mais de uma réplica web, ou um worker em processo separado, o SignalR exige um backplane compartilhado (ex.: Redis).** Uma conexão de cliente vive presa a **uma** réplica; um push originado em outra réplica (ou no worker) só alcança essa conexão através do backplane. Sem ele, a mensagem simplesmente não chega quando quem empurra não é a réplica que segura a conexão.

Este é exatamente o mesmo gatilho e a mesma família de risco de estado compartilhado entre réplicas já vista na chave de criptografia de sessão ([`10-configuracao-e-segredos.md`](10-configuracao-e-segredos.md)) e no L2 de cache ([`24-cache.md`](24-cache.md)): o critério é "alcançável por qualquer réplica", e reaproveitar um store que a aplicação já opera (o mesmo Redis do cache/sessão) evita introduzir peça de infraestrutura nova só para isso. Num sistema de réplica única e sem worker separado, o backplane não é necessário — mas essa é a mesma exceção temporária de [`15-observabilidade.md`](15-observabilidade.md): avalie se o gatilho já está presente hoje.

## A conexão é autenticada pela sessão, na mesma origem

O hub é hospedado pelo próprio `Api` (papel de BFF), sob a mesma origem do resto (`/hub/*` ao lado de `/api/*` e `/auth/*`, ver [`17-bff-angular-e-comunicacao.md`](17-bff-angular-e-comunicacao.md)). A conexão é autenticada pelo **mesmo cookie de sessão** de [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md) — o navegador o envia no handshake automaticamente, então não há token no frontend, coerente com o padrão BFF.

```csharp
[Authorize] // mesma sessão de cookie do resto da aplicação; ICurrentUser resolvido a partir dela
internal sealed class NotificacoesHub : Hub { }
```

**Regra direta: o handshake valida a origem (`Origin`) e revalida a sessão; uma sessão revogada derruba a conexão.** Como cookie é enviado automaticamente inclusive em conexão iniciada por outro site, o hub checa o header `Origin` no handshake (mesma origem, defesa contra *cross-site WebSocket hijacking*), análogo ao papel do CSRF em [`17-bff-angular-e-comunicacao.md`](17-bff-angular-e-comunicacao.md) para operação mutável. E a `SecurityVersion` de [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md) vale para a conexão viva: quando a sessão é revogada, a conexão persistente é encerrada, não deixada aberta até expirar sozinha.

## Mensagem sempre escopada ao ator; nunca broadcast de dado de domínio

**Regra direta: uma mensagem é entregue ao usuário dono do dado (`Clients.User(userId)`, com o `UserId` canônico de [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md)), nunca por broadcast a todos os clientes.** Enviar dado de domínio para todas as conexões reintroduz, no canal de push, o vazamento entre usuários que o filtro por linha ([`14-filtro-de-dados.md`](14-filtro-de-dados.md)) existe para impedir — o mesmo risco que a chave de cache escopada por ator cobre em [`24-cache.md`](24-cache.md).

Quando várias pessoas legitimamente observam o mesmo recurso (ex.: um painel compartilhado), use um **group** por recurso — mas a entrada no group é autorizada no momento da conexão, pela mesma regra de autorização por recurso de [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md); ninguém entra num group cujo recurso não teria direito de ler.

## Cliente Angular

O Angular usa o cliente oficial (`@microsoft/signalr`), na mesma origem, com o cookie de sessão enviado automaticamente — sem token, sem biblioteca de auth no cliente, coerente com [`17-bff-angular-e-comunicacao.md`](17-bff-angular-e-comunicacao.md):

```typescript
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/hub/notificacoes')        // mesma origem; cookie de sessão vai no handshake
  .withAutomaticReconnect()            // reconexão com backoff nativa
  .build();

connection.on('pedido.confirmado', ({ pedidoId }) => {
  // dica, não verdade: re-busca o detalhe pelo caminho normal de leitura
  this.pedidosService.recarregar(pedidoId);
});
```

Ao reconectar (`onreconnected`), o cliente re-sincroniza o estado por `Query`, cobrindo o que possa ter perdido enquanto esteve desconectado (regra de best-effort, acima).

## SignalR vs. SSE vs. WebSocket cru

| Mecanismo | Quando | Custo |
| --- | --- | --- |
| **SignalR** (default) | Caso geral: precisa de fallback de transporte, groups, reconexão gerenciada e backplane maduro para scale-out | Uma dependência e o backplane a operar quando há mais de uma réplica |
| **SSE** (Server-Sent Events) | Push **unidirecional** simples (servidor→cliente), sem groups, sem necessidade de fallback rico; HTTP nativo, atravessa proxy sem configuração especial | Mais simples no protocolo, mas o mesmo problema de backplane continua (a conexão ainda vive presa a uma réplica) |
| **WebSocket cru** | Só quando há requisito específico que SignalR/SSE não atendem | Reimplementa reconexão, fallback, roteamento por usuário à mão — evite por padrão |

**Regra direta: SignalR é o default; SSE é opção para o caso unidirecional simples; WebSocket cru é exceção justificada.** Escolher SSE simplifica o protocolo e o cliente, mas **não** dispensa o backplane em cenário multi-réplica/worker — a decisão de backplane é ortogonal à de protocolo.

## Observabilidade

A mensagem de push carrega o identificador de correlação da operação que a originou ([`15-observabilidade.md`](15-observabilidade.md)), religando o log/trace do push à requisição ou ao evento de worker que o disparou. Meça, no mínimo, conexões ativas e falhas de entrega/reconexão; um pico de reconexão costuma indicar problema de réplica ou de backplane, não de cliente.

## Nível de confiança

As regras de princípio — push como dica e não fonte de verdade, best-effort com estado autoritativo por `Query`, abstração `IUserNotifier` isolando o módulo de SignalR, escopo por ator, backplane obrigatório em multi-réplica — são síntese direta de princípios já estabelecidos nesta referência (isolamento de infraestrutura de [`11`](11-autenticacao-e-sessao.md), filtro de dado de [`14`](14-filtro-de-dados.md), estado compartilhado entre réplicas de [`10`](10-configuracao-e-segredos.md)/[`24`](24-cache.md)), com alta confiança. A escolha SignalR vs. SSE e o backplane concreto (Redis) são recomendações de plataforma, calibráveis por sistema.

## Nota de aplicação

Um sistema concreto pode divergir conscientemente, por exemplo:

- Usar **polling de endpoint de status** em vez de push, quando o único caso é "meu comando assíncrono terminou?" e a latência de alguns segundos é aceitável — menos infraestrutura, sem hub nem backplane.
- Adotar **SSE** em vez de SignalR quando o uso é estritamente unidirecional e simples, aceitando abrir mão de groups e fallback.
- Rodar sem backplane enquanto for genuinamente réplica única e sem worker separado (mesma exceção temporária de OpenTelemetry em [`15-observabilidade.md`](15-observabilidade.md)), migrando para backplane no momento em que a segunda réplica ou o worker aparecer.

## Veja também

- [`17-bff-angular-e-comunicacao.md`](17-bff-angular-e-comunicacao.md): request/response Angular ↔ BFF, mesma origem, sessão por cookie — este documento é o canal assíncrono complementar
- [`26-background-jobs-e-worker.md`](26-background-jobs-e-worker.md): o worker que, ao concluir, chama `IUserNotifier` para avisar o cliente
- [`25-transacao-e-unit-of-work.md`](25-transacao-e-unit-of-work.md): evento de domínio pós-commit que dispara a notificação em fluxo originado na web
- [`11-autenticacao-e-sessao.md`](11-autenticacao-e-sessao.md): sessão por cookie que autentica a conexão, `SecurityVersion` que a revoga
- [`14-filtro-de-dados.md`](14-filtro-de-dados.md): por que a mensagem é uma dica e o cliente re-busca por `Query`, mantendo o filtro num só lugar
- [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md): autorização de entrada num group por recurso
- [`10-configuracao-e-segredos.md`](10-configuracao-e-segredos.md) e [`24-cache.md`](24-cache.md): backplane como estado compartilhado entre réplicas, mesma família de decisão
