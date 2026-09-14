using Mediator;
using Microsoft.Extensions.Caching.Memory;

namespace SharedKernel.Messaging;

/// <summary>
/// Quinto elo da ordem fixa (Logging → Validation → Authorization → Idempotency → Caching →
/// Handler, arquitetura/03-commands-e-queries.md, arquitetura/24-cache.md). Roda só para
/// TMessage : ICacheableQuery — hoje só CategoriasDeServicoQuery (Catalogo) usa o marcador.
///
/// Abstração: IMemoryCache (Microsoft.Extensions.Caching.Memory), não HybridCache. Divergência
/// consciente do exemplo de referência de arquitetura/24-cache.md (que sugere HybridCache/.NET
/// 9+ com L1+L2 e invalidação por tag nativa via RemoveByTagAsync): esta solution ainda não tem
/// um store L2 compartilhado (Redis) provisionado para módulos de leitura, e o próprio
/// documento 24 aceita IMemoryCache por réplica "quando o dado tolera divergência entre
/// réplicas e a invalidação não precisa ser imediata em todas" — caso de
/// CategoriasDeServicoQuery (catálogo de referência, baixa frequência de mudança). Reavaliar
/// para HybridCache/L2 quando um módulo precisar de invalidação por tag disparada por Command
/// entre réplicas (nenhum Command invalida cache nesta rodada).
///
/// TTL fixo de 60s (ponto de ajuste por Query se necessário — hoje só uma Query cacheável,
/// então não há por onde variar TTL por caso de uso ainda; quando isso for necessário, o TTL
/// vira propriedade de ICacheableQuery em vez de constante deste behavior).
///
/// Chave física NÃO inclui escopo de ator: arquitetura/24 exige isso só quando o dado tem
/// escopo por usuário. CategoriasDeServicoQuery é dado global (catálogo de referência, mesmo
/// resultado para qualquer autenticado — ver CategoriasDeServicoQuery, "papel: qualquer
/// autenticado"), então a chave usa só message.CacheKey. Se uma Query cacheável futura tiver
/// escopo por ator, este behavior precisa ganhar a mesma regra de composição de chave do
/// documento 24 antes de ser usada para ela — não implementado agora por não haver caso real.
/// </summary>
public sealed class CachingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage, ICacheableQuery
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;

    public CachingBehavior(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(message.CacheKey, out TResponse? cached) && cached is not null)
        {
            return cached;
        }

        var response = await next(message, cancellationToken);

        _cache.Set(message.CacheKey, response, Ttl);

        return response;
    }
}
