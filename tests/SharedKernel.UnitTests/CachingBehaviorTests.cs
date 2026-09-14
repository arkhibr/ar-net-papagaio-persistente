using Mediator;
using Microsoft.Extensions.Caching.Memory;
using SharedKernel;
using SharedKernel.Messaging;
using Xunit;

namespace SharedKernel.UnitTests;

/// <summary>
/// CachingBehavior (arquitetura/24-cache.md, arquitetura/03-commands-e-queries.md):
///
/// 1. Segunda chamada com a mesma CacheKey não deve executar o handler de novo (devolve o
///    valor cacheado).
/// 2. Chaves de cache diferentes não compartilham entrada (chamam o handler para cada uma).
/// </summary>
public class CachingBehaviorTests
{
    private sealed record MensagemDeTeste(string CacheKey) : IRequest<Result<Guid>>, ICacheableQuery;

    [Fact]
    public async Task Segunda_chamada_com_a_mesma_CacheKey_nao_deve_executar_o_handler_de_novo()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var behavior = new CachingBehavior<MensagemDeTeste, Result<Guid>>(cache);
        var mensagem = new MensagemDeTeste("categorias-de-servico");
        var chamadasAoHandler = 0;
        var valorGerado = Guid.NewGuid();

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next = (_, _) =>
        {
            chamadasAoHandler++;
            return ValueTask.FromResult(Result<Guid>.Success(valorGerado));
        };

        var primeiraResposta = await behavior.Handle(mensagem, next, CancellationToken.None);
        var segundaResposta = await behavior.Handle(mensagem, next, CancellationToken.None);

        Assert.Equal(1, chamadasAoHandler);
        Assert.Equal(valorGerado, primeiraResposta.Value);
        Assert.Equal(valorGerado, segundaResposta.Value);
    }

    [Fact]
    public async Task Chaves_de_cache_diferentes_devem_chamar_o_handler_para_cada_uma()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var behavior = new CachingBehavior<MensagemDeTeste, Result<Guid>>(cache);
        var mensagemA = new MensagemDeTeste("chave-a");
        var mensagemB = new MensagemDeTeste("chave-b");
        var chamadasAoHandler = 0;

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next = (_, _) =>
        {
            chamadasAoHandler++;
            return ValueTask.FromResult(Result<Guid>.Success(Guid.NewGuid()));
        };

        await behavior.Handle(mensagemA, next, CancellationToken.None);
        await behavior.Handle(mensagemB, next, CancellationToken.None);

        Assert.Equal(2, chamadasAoHandler);
    }
}
