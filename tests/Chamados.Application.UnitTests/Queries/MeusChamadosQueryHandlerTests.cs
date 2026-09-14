using Chamados.Application.Queries;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Chamados.Domain;
using Xunit;

namespace Chamados.Application.UnitTests.Queries;

/// <summary>
/// MeusChamadosQueryHandler (modo TDD): filtro por linha (arquitetura/12/14) — nunca aceita
/// um SolicitanteId vindo de fora, sempre resolve currentUser.UserId internamente. O teste
/// central aqui é justamente que o handler nunca devolve chamado de outro solicitante,
/// mesmo que a Query em si não carregue um SolicitanteId explícito.
/// </summary>
public class MeusChamadosQueryHandlerTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Deve_devolver_somente_os_chamados_do_proprio_solicitante_autenticado()
    {
        var solicitanteAutenticado = Guid.NewGuid();
        var outroSolicitante = Guid.NewGuid();

        var chamadoDoProprioUsuario = Chamado.Abrir(
            solicitanteAutenticado, Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        var chamadoDeOutraPessoa = Chamado.Abrir(
            outroSolicitante, Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);

        var repository = new FakeChamadoRepository()
            .ComChamado(chamadoDoProprioUsuario)
            .ComChamado(chamadoDeOutraPessoa);

        var currentUser = new FakeCurrentUser { UserId = solicitanteAutenticado };
        var handler = new MeusChamadosQueryHandler(repository, currentUser);

        var resultado = await handler.Handle(new MeusChamadosQuery(Page: 1, PageSize: 20), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var chamado = Assert.Single(resultado.Value!);
        Assert.Equal(chamadoDoProprioUsuario.Id, chamado.Id);
    }
}
