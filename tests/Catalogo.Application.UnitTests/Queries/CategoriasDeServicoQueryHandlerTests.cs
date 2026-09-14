using Catalogo.Application.Queries;
using Catalogo.Application.UnitTests.Fakes;
using Catalogo.Contracts;
using Catalogo.Domain;
using Xunit;

namespace Catalogo.Application.UnitTests.Queries;

/// <summary>
/// CategoriasDeServicoQueryHandler (modo TDD): lista todas as categorias cadastradas.
/// Autorização "qualquer autenticado" é leitura A (arquitetura/12), resolvida na Api via
/// [Authorize] genérico — não há checagem de autorização por recurso a testar aqui, só o
/// comportamento de listagem em si.
/// </summary>
public class CategoriasDeServicoQueryHandlerTests
{
    [Fact]
    public async Task Deve_listar_todas_as_categorias_cadastradas()
    {
        var categoriaRede = CategoriaDeServico.Criar("Suporte de Rede", Guid.NewGuid());
        var categoriaHardware = CategoriaDeServico.Criar("Suporte de Hardware", Guid.NewGuid());
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
}
