using Microsoft.Extensions.Caching.Memory;

namespace SharedKernel.Messaging;

/// <summary>Implementação sobre o mesmo IMemoryCache e a mesma chave física do CachingBehavior.</summary>
internal sealed class MemoryCacheInvalidator : ICacheInvalidator
{
    private readonly IMemoryCache _cache;

    public MemoryCacheInvalidator(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void InvalidarGlobal<TQuery>(string cacheKey) where TQuery : IGlobalCacheableQuery =>
        _cache.Remove(ChaveDeCache.Global(typeof(TQuery), cacheKey));
}

/// <summary>Chave física de cache, única fonte da fórmula para o behavior e para a invalidação.</summary>
internal static class ChaveDeCache
{
    public static string Global(Type tipoDaQuery, string cacheKey) => $"{tipoDaQuery.FullName}:{cacheKey}";
}
