using Catalogo.Application.Queries;
using Catalogo.Application.UnitTests.Fakes;
using Catalogo.Contracts;
using Xunit;

namespace Catalogo.Application.UnitTests.Queries;

/// <summary>
/// ResolverEquipeDoTecnicoQueryHandler (modo TDD): implementação real da Query pública
/// consumida pela implementação de Chamados.Application.IEquipeDoUsuarioResolver, usada por
/// FilaDaEquipeQueryHandler para resolver a equipe do técnico/supervisor autenticado (filtro
/// por linha, arquitetura/14-filtro-de-dados.md) — nunca aceita EquipeId vinda do cliente.
/// </summary>
public class ResolverEquipeDoTecnicoQueryHandlerTests
{
    [Fact]
    public async Task Tecnico_vinculado_deve_devolver_a_equipe_correspondente()
    {
        var tecnicoId = Guid.NewGuid();
        var equipeId = Guid.NewGuid();
        var repository = new FakeMembroDeEquipeRepository().ComVinculo(tecnicoId, equipeId);
        var handler = new ResolverEquipeDoTecnicoQueryHandler(repository);

        var resultado = await handler.Handle(
            new ResolverEquipeDoTecnicoQuery(tecnicoId), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(equipeId, resultado.Value);
    }

    [Fact]
    public async Task Tecnico_sem_vinculo_deve_devolver_sucesso_com_valor_nulo()
    {
        var repository = new FakeMembroDeEquipeRepository();
        var handler = new ResolverEquipeDoTecnicoQueryHandler(repository);

        var resultado = await handler.Handle(
            new ResolverEquipeDoTecnicoQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Null(resultado.Value);
    }
}
