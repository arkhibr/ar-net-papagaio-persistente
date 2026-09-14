using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>ReabrirChamadoCommand.IsAuthorizedAsync: só o solicitante.</summary>
public class ReabrirChamadoCommandAuthorizationTests
{
    [Fact]
    public async Task O_proprio_solicitante_deve_ser_autorizado()
    {
        var solicitanteId = Guid.NewGuid();
        var command = new ReabrirChamadoCommand(Guid.NewGuid(), solicitanteId, "chave-reabrir");
        var currentUser = new FakeCurrentUser { UserId = solicitanteId };
        var context = new FakeAuthorizationContext();

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Usuario_diferente_do_solicitante_nao_deve_ser_autorizado()
    {
        var solicitanteId = Guid.NewGuid();
        var command = new ReabrirChamadoCommand(Guid.NewGuid(), solicitanteId, "chave-reabrir");
        var currentUser = new FakeCurrentUser();
        var context = new FakeAuthorizationContext();

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }

    [Fact]
    public async Task Ator_de_sistema_nao_deve_ser_autorizado_a_reabrir()
    {
        var solicitanteId = Guid.NewGuid();
        var command = new ReabrirChamadoCommand(Guid.NewGuid(), solicitanteId, "chave-reabrir");
        var currentUser = FakeCurrentUser.SistemaAutomatizado();
        var context = new FakeAuthorizationContext();

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }
}
