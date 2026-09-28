using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// ResolverChamadoCommand.IsAuthorizedAsync (arquitetura/12): só o técnico atualmente
/// atribuído (EhTecnicoAtribuidoAsync).
/// </summary>
public class ResolverChamadoCommandAuthorizationTests
{
    [Fact]
    public async Task Tecnico_atribuido_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = new ResolverChamadoCommand(chamadoId, "Resolvido.", "chave-resolver");
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var context = new FakeAuthorizationContext().ComTecnicoAtribuido(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Tecnico_atribuido_a_outro_chamado_nao_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = new ResolverChamadoCommand(chamadoId, "Resolvido.", "chave-resolver");
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var context = new FakeAuthorizationContext().ComTecnicoAtribuido(currentUser.UserId, Guid.NewGuid());

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }
}
