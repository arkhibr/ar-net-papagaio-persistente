using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// ReabrirChamadoCommand.IsAuthorizedAsync (arquitetura/12): só o solicitante, conferido no
/// estado persistido via EhSolicitanteAsync. Ator de sistema não reabre.
/// </summary>
public class ReabrirChamadoCommandAuthorizationTests
{
    [Fact]
    public async Task O_proprio_solicitante_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = new ReabrirChamadoCommand(chamadoId, "chave-reabrir");
        var currentUser = new FakeCurrentUser();
        var context = new FakeAuthorizationContext().ComSolicitante(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Usuario_diferente_do_solicitante_nao_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = new ReabrirChamadoCommand(chamadoId, "chave-reabrir");
        var currentUser = new FakeCurrentUser();
        var context = new FakeAuthorizationContext().ComSolicitante(Guid.NewGuid(), chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }

    [Fact]
    public async Task Ator_de_sistema_nao_deve_ser_autorizado_a_reabrir()
    {
        var command = new ReabrirChamadoCommand(Guid.NewGuid(), "chave-reabrir");
        var currentUser = FakeCurrentUser.SistemaAutomatizado();
        var context = new FakeAuthorizationContext();

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }
}
