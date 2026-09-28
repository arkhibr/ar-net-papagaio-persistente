using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// DevolverChamadoCommand.IsAuthorizedAsync (arquitetura/12): só o técnico atualmente
/// atribuído (EhTecnicoAtribuidoAsync). Ser membro da equipe não basta.
/// </summary>
public class DevolverChamadoCommandAuthorizationTests
{
    [Fact]
    public async Task Tecnico_atribuido_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = new DevolverChamadoCommand(chamadoId, "chave-devolver");
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var context = new FakeAuthorizationContext().ComTecnicoAtribuido(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Membro_da_equipe_que_nao_e_o_tecnico_atribuido_nao_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = new DevolverChamadoCommand(chamadoId, "chave-devolver");
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var context = new FakeAuthorizationContext().ComMembroDaEquipeResponsavel(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }
}
