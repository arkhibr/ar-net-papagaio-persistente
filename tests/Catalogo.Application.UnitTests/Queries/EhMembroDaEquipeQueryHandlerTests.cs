using Catalogo.Application.Queries;
using Catalogo.Application.UnitTests.Fakes;
using Catalogo.Contracts;
using Xunit;

namespace Catalogo.Application.UnitTests.Queries;

/// <summary>
/// EhMembroDaEquipeQueryHandler: implementação real da Query pública consumida pela
/// implementação de Chamados.Contracts.IChamadosAuthorizationContext (Infrastructure de
/// Chamados), via ISender (arquitetura/04).
/// Checagem determinística de vínculo: sempre Result.Success (true ou false).
/// </summary>
public class EhMembroDaEquipeQueryHandlerTests
{
    [Fact]
    public async Task Tecnico_vinculado_a_equipe_deve_devolver_true()
    {
        var tecnicoId = Guid.NewGuid();
        var equipeId = Guid.NewGuid();
        var repository = new FakeMembroDeEquipeRepository().ComVinculo(tecnicoId, equipeId);
        var handler = new EhMembroDaEquipeQueryHandler(repository);

        var resultado = await handler.Handle(
            new EhMembroDaEquipeQuery(tecnicoId, equipeId), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.True(resultado.Value);
    }

    [Fact]
    public async Task Tecnico_vinculado_a_outra_equipe_deve_devolver_false()
    {
        var tecnicoId = Guid.NewGuid();
        var equipeVinculada = Guid.NewGuid();
        var outraEquipe = Guid.NewGuid();
        var repository = new FakeMembroDeEquipeRepository().ComVinculo(tecnicoId, equipeVinculada);
        var handler = new EhMembroDaEquipeQueryHandler(repository);

        var resultado = await handler.Handle(
            new EhMembroDaEquipeQuery(tecnicoId, outraEquipe), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value);
    }

    [Fact]
    public async Task Tecnico_sem_nenhum_vinculo_deve_devolver_false()
    {
        var repository = new FakeMembroDeEquipeRepository();
        var handler = new EhMembroDaEquipeQueryHandler(repository);

        var resultado = await handler.Handle(
            new EhMembroDaEquipeQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.False(resultado.Value);
    }
}
