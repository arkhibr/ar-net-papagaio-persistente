using Mediator;
using Microsoft.Extensions.Caching.Memory;
using SharedKernel.Messaging;
using Xunit;

namespace SharedKernel.UnitTests;

/// <summary>
/// CachingBehavior (arquitetura/24-cache.md; A6 de achados.md): escopo por ator é o padrão;
/// só IGlobalCacheableQuery compartilha a entrada entre usuários.
/// </summary>
public class CachingBehaviorTests
{
    private sealed record QueryPorAtor(string CacheKey) : IRequest<Result<Guid>>, ICacheableQuery;

    private sealed record QueryGlobal(string CacheKey) : IRequest<Result<Guid>>, IGlobalCacheableQuery;

    private sealed record OutraQueryPorAtor(string CacheKey) : IRequest<Result<Guid>>, ICacheableQuery;

    private static async Task<(Result<Guid> Resposta, int Chamadas)> ExecutarAsync<TQuery>(
        IMemoryCache cache, ICurrentUser ator, TQuery query, int chamadasAntes)
        where TQuery : IMessage, ICacheableQuery
    {
        var chamadas = chamadasAntes;
        var behavior = new CachingBehavior<TQuery, Result<Guid>>(cache, ator);
        var resposta = await behavior.Handle(query, (_, _) =>
        {
            chamadas++;
            return ValueTask.FromResult(Result<Guid>.Success(Guid.NewGuid()));
        }, CancellationToken.None);
        return (resposta, chamadas);
    }

    [Fact]
    public async Task Mesmo_usuario_e_mesma_chave_nao_executa_o_handler_de_novo()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var ator = new FakeCurrentUser();

        var (primeira, chamadas) = await ExecutarAsync(cache, ator, new QueryPorAtor("k"), 0);
        var (segunda, total) = await ExecutarAsync(cache, ator, new QueryPorAtor("k"), chamadas);

        Assert.Equal(1, total);
        Assert.Equal(primeira.Value, segunda.Value);
    }

    [Fact]
    public async Task Por_padrao_usuarios_diferentes_nunca_compartilham_a_entrada()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var (deA, chamadas) = await ExecutarAsync(cache, new FakeCurrentUser(), new QueryPorAtor("k"), 0);
        var (deB, total) = await ExecutarAsync(cache, new FakeCurrentUser(), new QueryPorAtor("k"), chamadas);

        Assert.Equal(2, total);
        Assert.NotEqual(deA.Value, deB.Value);
    }

    [Fact]
    public async Task Query_global_declarada_compartilha_a_entrada_entre_usuarios()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var (deA, chamadas) = await ExecutarAsync(cache, new FakeCurrentUser(), new QueryGlobal("k"), 0);
        var (deB, total) = await ExecutarAsync(cache, new FakeCurrentUser(), new QueryGlobal("k"), chamadas);

        Assert.Equal(1, total);
        Assert.Equal(deA.Value, deB.Value);
    }

    [Fact]
    public async Task Queries_diferentes_com_a_mesma_CacheKey_nao_se_sobrescrevem()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var ator = new FakeCurrentUser();

        var (_, chamadas) = await ExecutarAsync(cache, ator, new QueryPorAtor("k"), 0);
        var (_, total) = await ExecutarAsync(cache, ator, new OutraQueryPorAtor("k"), chamadas);

        Assert.Equal(2, total);
    }
}
