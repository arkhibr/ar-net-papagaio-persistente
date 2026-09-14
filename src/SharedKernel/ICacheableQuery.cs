namespace SharedKernel;

/// <summary>
/// Marca uma Query cujo resultado pode passar pelo CachingBehavior do pipeline
/// (arquitetura/24-cache.md, arquitetura/03-commands-e-queries.md). Opt-in por Query.
/// </summary>
public interface ICacheableQuery
{
    string CacheKey { get; }
}
