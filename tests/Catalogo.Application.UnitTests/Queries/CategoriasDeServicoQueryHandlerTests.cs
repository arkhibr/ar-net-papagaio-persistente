using Catalogo.Application.Queries;
using Catalogo.Application.UnitTests.Fakes;
using Catalogo.Contracts;
using Catalogo.Domain;
using SharedKernel;
using Xunit;

namespace Catalogo.Application.UnitTests.Queries;

/// <summary>
/// CategoriasDeServicoQueryHandler: lista todas as categorias cadastradas, projetadas pelo
/// repositório. Autorização "qualquer autenticado" é leitura A (arquitetura/12), na Api. A
/// Query é IGlobalCacheableQuery (arquitetura/24): o dado é o mesmo para qualquer usuário, e a
/// omissão do escopo de ator no cache é decisão explícita da Query.
/// </summary>
public class CategoriasDeServicoQueryHandlerTests
{
    [Fact]
    public async Task Deve_listar_todas_as_categorias_cadastradas()
    {
        var categoriaRede = CategoriaDeServico.Criar("Suporte de Rede", Guid.NewGuid(), [(PrioridadeServico.Media, 24)]);
        var categoriaHardware = CategoriaDeServico.Criar("Suporte de Hardware", Guid.NewGuid(), [(PrioridadeServico.Media, 24)]);
        var repository = new FakeCategoriaDeServicoRepository()
            .ComCategoria(categoriaRede)
            .ComCategoria(categoriaHardware);
        var handler = new CategoriasDeServicoQueryHandler(repository);

        var resultado = await handler.Handle(new CategoriasDeServicoQuery(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(2, resultado.Value!.Count);
        Assert.Contains(resultado.Value!, c => c.Nome == "Suporte de Rede");
        Assert.Contains(resultado.Value!, c => c.Nome == "Suporte de Hardware");
    }

    [Fact]
    public async Task Sem_categorias_cadastradas_deve_devolver_lista_vazia_com_sucesso()
    {
        var repository = new FakeCategoriaDeServicoRepository();
        var handler = new CategoriasDeServicoQueryHandler(repository);

        var resultado = await handler.Handle(new CategoriasDeServicoQuery(), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(resultado.Value!);
    }

    [Fact]
    public void Query_deve_ser_cacheavel_globalmente_com_chave_fixa()
    {
        var query = new CategoriasDeServicoQuery();

        Assert.IsAssignableFrom<IGlobalCacheableQuery>(query);
        Assert.Equal("categorias-de-servico", query.CacheKey);
    }
}
