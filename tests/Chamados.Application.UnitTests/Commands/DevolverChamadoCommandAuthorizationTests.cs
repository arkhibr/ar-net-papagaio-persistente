using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>DevolverChamadoCommand.IsAuthorizedAsync: só o técnico atualmente atribuído.</summary>
public class DevolverChamadoCommandAuthorizationTests
{
    [Fact]
    public async Task Tecnico_atribuido_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = new DevolverChamadoCommand(chamadoId, "chave-devolver");
        var currentUser = new FakeCurrentUser();
        var context = new FakeAuthorizationContext().ComVinculo(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Tecnico_diferente_do_atribuido_nao_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = new DevolverChamadoCommand(chamadoId, "chave-devolver");
        var currentUser = new FakeCurrentUser();
        var context = new FakeAuthorizationContext(); // sem vínculo para este usuário

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }
}
