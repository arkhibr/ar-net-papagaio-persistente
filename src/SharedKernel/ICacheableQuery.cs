namespace SharedKernel;

/// <summary>
/// Marca uma Query cujo resultado pode passar pelo CachingBehavior (arquitetura/24-cache.md).
/// Por padrão a chave física inclui o usuário autenticado (escopo por ator). Para dado
/// comprovadamente global, implemente IGlobalCacheableQuery.
/// </summary>
public interface ICacheableQuery
{
    string CacheKey { get; }
}

/// <summary>
/// Declaração explícita de que o resultado é o mesmo para qualquer usuário e pode ser
/// compartilhado no cache sem escopo de ator (arquitetura/24-cache.md, "Chave sempre com o
/// escopo do ator": a omissão do escopo é decisão visível da Query, nunca o padrão).
/// </summary>
public interface IGlobalCacheableQuery : ICacheableQuery;
