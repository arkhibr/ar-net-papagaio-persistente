using Mediator;
using Microsoft.Extensions.Caching.Memory;

namespace SharedKernel.Messaging;

/// <summary>
/// Quinto elo da ordem fixa (arquitetura/03-commands-e-queries.md, arquitetura/24-cache.md).
/// Roda só para TMessage : ICacheableQuery.
///
/// Chave física: tipo da Query + CacheKey + usuário autenticado. O escopo por ator é o padrão;
/// só uma Query que implementa IGlobalCacheableQuery compartilha a entrada entre usuários
/// (arquitetura/24, "Chave sempre com o escopo do ator"). O prefixo com o tipo evita que duas
/// Queries com a mesma CacheKey sobrescrevam uma à outra.
///
/// Abstração: IMemoryCache, não HybridCache. Divergência consciente do exemplo de referência de
/// arquitetura/24 (sem store L2 provisionado); o próprio documento aceita IMemoryCache por réplica
/// quando o dado tolera divergência entre réplicas. TTL fixo de 60s até existir uma segunda Query
/// cacheável (B7 de achados.md).
/// </summary>
public sealed class CachingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage, ICacheableQuery
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;
    private readonly ICurrentUser _currentUser;

    public CachingBehavior(IMemoryCache cache, ICurrentUser currentUser)
    {
        _cache = cache;
        _currentUser = currentUser;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var chave = ChaveFisica(message);

        if (_cache.TryGetValue(chave, out TResponse? cached) && cached is not null)
        {
            return cached;
        }

        var response = await next(message, cancellationToken);

        _cache.Set(chave, response, Ttl);

        return response;
    }

    private string ChaveFisica(TMessage message)
    {
        var chave = ChaveDeCache.Global(typeof(TMessage), message.CacheKey);

        return message is IGlobalCacheableQuery ? chave : $"{chave}:ator={_currentUser.UserId}";
    }
}
