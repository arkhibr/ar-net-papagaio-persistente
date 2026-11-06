namespace SharedKernel;

/// <summary>
/// Remove do cache a entrada de uma Query global (IGlobalCacheableQuery) depois de um Command
/// que muda o dado dela (arquitetura/24-cache.md, invalidação por escrita). Enquanto o
/// CachingBehavior usar IMemoryCache por réplica, a remoção vale só na réplica que executou o
/// Command; as outras convergem no TTL (B7 de achados.md: tags e L2 ficam para depois).
/// </summary>
public interface ICacheInvalidator
{
    void InvalidarGlobal<TQuery>(string cacheKey) where TQuery : IGlobalCacheableQuery;
}
