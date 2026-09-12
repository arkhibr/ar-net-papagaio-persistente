# Cache

> Preenche uma promessa que o pipeline já fazia sem documento por trás: o `CachingBehavior` aparece na ordem fixa em [`03-commands-e-queries.md`](03-commands-e-queries.md), [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md) e [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md), mas o que ele cacheia, com que chave, por quanto tempo e como invalida nunca foi especificado. Este documento fecha isso. Complementa [`14-filtro-de-dados.md`](14-filtro-de-dados.md) (visibilidade por ator, de onde vem a regra de segurança de chave de cache) e [`27-leitura-e-query-side.md`](27-leitura-e-query-side.md) (que aponta a `Query` como o que pode ser cacheado).

## Cache é opt-in por `Query`, nunca automático

**Regra direta: só uma `Query` que implementa o marcador `ICacheableQuery` é cacheada. Nenhum `Command` é cacheado, nunca.** O `CachingBehavior` continua na posição fixa do pipeline (`Logging → Validation → Authorization → Idempotency → Caching → Handler`), mas só faz algo para mensagens que carregam o marcador; para todo o resto, ele repassa direto para o próximo elo. É a mesma mecânica de opt-in já usada por `IIdempotentCommand` ([`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md)) e `IRequiresAuthorization` ([`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md)): o behavior existe uma vez, o marcador decide caso a caso se ele atua.

Cachear tudo por padrão troca um problema visível (latência de leitura) por um invisível e pior (resposta obsoleta, ou pior, resposta de um usuário servida a outro — ver "Chave sempre com o escopo do ator", abaixo). Cache é decisão consciente por caso de uso, motivada por medição, não default.

```csharp
public interface ICacheableQuery
{
    // Chave estável e única do caso de uso + seus parâmetros normalizados.
    // NÃO inclui o escopo do ator: isso o behavior acrescenta (ver regra de segurança).
    string ChaveDeCache { get; }

    // TTL decidido pelo próprio caso de uso, no servidor, nunca pelo cliente.
    TimeSpan Expiracao { get; }

    // Tags para invalidação em lote quando um Command muda o dado por trás.
    IReadOnlyCollection<string> TagsDeCache { get; }
}
```

## Chave sempre com o escopo do ator (regra de segurança, não de performance)

**Regra direta: quando o dado da `Query` tem escopo por ator (o caso normal, ver filtro por linha em [`14-filtro-de-dados.md`](14-filtro-de-dados.md)), a chave física de cache inclui o identificador do ator (`ICurrentUser.UserId`), acrescentado pelo `CachingBehavior`, nunca deixado a cargo do `ChaveDeCache` da `Query`.** Sem isso, a primeira requisição de um usuário popula o cache e a próxima requisição de **outro** usuário, com a mesma `ChaveDeCache` lógica, recebe o dado do primeiro. Isso não é resposta obsoleta: é vazamento de dado entre usuários, a mesma falha que o filtro por linha existe para impedir, reintroduzida na camada de cache.

O behavior compõe a chave física assim: `{ChaveDeCache da Query} + {escopo do ator}`. O escopo do ator é omitido só quando o dado é comprovadamente global (não varia por quem pergunta — ex.: uma lista de referência pública), e essa omissão é uma decisão explícita e visível da `Query` (um marcador adicional, `IGlobalCacheableQuery`, ou uma propriedade `EscopoGlobal => true`), nunca o comportamento padrão. Na dúvida, a chave é escopada por ator.

```csharp
public sealed class CachingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : ICacheableQuery
{
    private readonly HybridCache _cache;
    private readonly ICurrentUser _currentUser;

    public CachingBehavior(HybridCache cache, ICurrentUser currentUser)
    {
        _cache = cache;
        _currentUser = currentUser;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Escopo do ator embutido na chave física: nunca deixado a cargo da Query.
        var chaveFisica = $"{message.ChaveDeCache}:ator={_currentUser.UserId}";

        return await _cache.GetOrCreateAsync(
            chaveFisica,
            async ct => await next(message, ct),
            options: new HybridCacheEntryOptions { Expiration = message.Expiracao },
            tags: message.TagsDeCache,
            cancellationToken: cancellationToken);
    }
}
```

## Por que a posição no pipeline já resolve autorização

O `CachingBehavior` vem **depois** de `AuthorizationBehavior` na ordem fixa. Consequência direta: uma requisição não autorizada é rejeitada por `AuthorizationDeniedException` antes de chegar ao cache — nunca popula nem serve uma entrada. Não é preciso replicar checagem de autorização dentro do cache; a ordem do pipeline já garante que só requisição autorizada alcança essa etapa (mesmo raciocínio de por que uma tentativa não autorizada não consome `Idempotency-Key`, ver [`03-commands-e-queries.md`](03-commands-e-queries.md)). O escopo por ator na chave (seção anterior) cobre o eixo ortogonal: dois usuários **ambos autorizados** não podem compartilhar a mesma entrada física.

## Invalidação por tag, disparada pela escrita

**Regra direta: quem invalida o cache é o `Command` que muda o dado por trás dele, por tag, na mesma fronteira transacional da escrita (ver [`25-transacao-e-unit-of-work.md`](25-transacao-e-unit-of-work.md)), nunca um TTL curto usado como substituto de invalidação.** TTL é a rede de segurança para o que escapou (ou para dado que tolera janela de obsolescência), não o mecanismo primário.

O módulo dono do dado invalida as próprias tags depois de um `SaveChanges` bem-sucedido — nunca antes, senão uma escrita que falha por concorrência ([`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md)) teria invalidado cache de um dado que não mudou:

```csharp
// Depois do commit da operação que renomeia o recurso:
await _cache.RemoveByTagAsync($"recurso:{recursoId}", cancellationToken);
```

A tag conecta a `Query` (que a declara em `TagsDeCache`) e o `Command` (que a remove): as duas pontas usam a mesma convenção de nome de tag, `{tipoDeRecurso}:{id}`, para que a escrita saiba qual leitura invalidar sem conhecer cada `Query` individualmente.

## Abstração de cache

**Regra direta: a abstração default é `HybridCache` (.NET 9+).** Ela combina cache em memória (L1) e cache distribuído (L2) atrás de uma única API, resolve *cache stampede* (várias requisições recalculando a mesma chave ao mesmo tempo) nativamente, e suporta invalidação por tag (`RemoveByTagAsync`) — os três requisitos deste documento. O store L2 concreto (Redis, ou o mesmo banco relacional já operado) é escolha de infraestrutura de deploy, não de arquitetura de código, mesmo critério da chave de sessão em [`10-configuracao-e-segredos.md`](10-configuracao-e-segredos.md): o que importa é ser alcançável por qualquer réplica, não a tecnologia.

Cache puramente em memória (`IMemoryCache`) por réplica é aceitável só quando o dado tolera divergência entre réplicas e a invalidação não precisa ser imediata em todas — caso raro; na dúvida, `HybridCache` com L2 compartilhado.

## Cache HTTP é outra camada, não substitui esta

O cache deste documento é de **resultado de `Query` dentro do processo** (server-side, por ator). É complementar, não concorrente, ao cache de saída HTTP (`Output Caching` do ASP.NET Core) e a headers `Cache-Control`/`ETag` na borda. Output caching serve resposta HTTP inteira antes mesmo de a requisição entrar no pipeline do Mediator, e por isso é apropriado só para conteúdo genuinamente público e não escopado por ator — a mesma regra de segurança da chave se aplica com ainda menos margem de erro. Para dado escopado por usuário, o cache no pipeline (este documento) é o lugar correto, porque o escopo do ator já está resolvido em `ICurrentUser`.

## Cache e leitura entre módulos

Um módulo cacheia o resultado das **próprias** `Query`s, atrás do próprio `Contracts`. Quando o módulo A consulta uma `Query` do `Contracts` do módulo B (ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md)), quem decide cachear é o módulo B (dono do dado e das tags de invalidação), não o A. O chamador nunca cacheia por conta própria o DTO alheio: se o fizesse, ficaria com uma cópia que o módulo dono não sabe invalidar quando o dado muda — a mesma razão pela qual o filtro por campo não é refeito do lado do chamador em [`14-filtro-de-dados.md`](14-filtro-de-dados.md).

Se a leitura entre módulos precisa de cópia local sincronizada, isso não é cache: é read model dedicado, populado por projeção via Outbox (ver [`04-comunicacao-entre-modulos.md`](04-comunicacao-entre-modulos.md), "Projeção via fila"). Cache expira e recalcula da fonte; read model é atualizado por evento e é fonte de leitura por si. Não confunda os dois.

## Nível de confiança

O mecanismo de marcador + behavior + tag é aplicação direta dos padrões já estabelecidos nesta referência (opt-in por interface, ordem fixa de pipeline). A escolha de `HybridCache` como default reflete o estado da plataforma em .NET 9+; um sistema em runtime anterior usa `IDistributedCache` + `IMemoryCache` combinados manualmente, com o mesmo desenho de chave e invalidação. Os limiares concretos (TTL por caso de uso, quando um cache "se paga") são calibração de cada sistema, guiada por medição, nunca número fixo desta referência.

## Nota de aplicação

Um sistema concreto pode divergir conscientemente, por exemplo:

- Usar `IDistributedCache`/`IMemoryCache` diretamente quando o runtime é anterior ao .NET 9, mantendo o desenho de chave escopada por ator e invalidação por convenção de nome (sem o `RemoveByTagAsync` nativo, a invalidação em lote é implementada sobre um índice de chaves por tag).
- Não adotar cache algum enquanto a medição não mostrar latência de leitura como problema real: a ausência do marcador em qualquer `Query` deixa o `CachingBehavior` inerte, sem custo.

## Veja também

- [`03-commands-e-queries.md`](03-commands-e-queries.md): ordem fixa do pipeline onde o `CachingBehavior` mora
- [`06-contrato-erro-http-idempotencia-e-concorrencia.md`](06-contrato-erro-http-idempotencia-e-concorrencia.md): mesmo padrão opt-in de `IIdempotentCommand`; por que só falha determinística é cacheável
- [`12-autorizacao-por-recurso.md`](12-autorizacao-por-recurso.md): por que a posição depois de `AuthorizationBehavior` já cobre o eixo de autorização
- [`14-filtro-de-dados.md`](14-filtro-de-dados.md): filtro por linha/ator, de onde vem a regra de escopo da chave de cache
- [`25-transacao-e-unit-of-work.md`](25-transacao-e-unit-of-work.md): a fronteira transacional em que a invalidação por tag é disparada
- [`27-leitura-e-query-side.md`](27-leitura-e-query-side.md): a `Query` como unidade cacheável
- [`10-configuracao-e-segredos.md`](10-configuracao-e-segredos.md): store externo alcançável por qualquer réplica, mesmo critério do L2 de cache
