using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>ResolverChamadoCommand.IsAuthorizedAsync: só o técnico atualmente atribuído.</summary>
public class ResolverChamadoCommandAuthorizationTests
{
    [Fact]
    public async Task Tecnico_atribuido_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = new ResolverChamadoCommand(chamadoId, "Resolvido.", "chave-resolver");
        var currentUser = new FakeCurrentUser();
        var context = new FakeAuthorizationContext().ComVinculo(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Tecnico_sem_vinculo_nao_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = new ResolverChamadoCommand(chamadoId, "Resolvido.", "chave-resolver");
        var currentUser = new FakeCurrentUser();
        var context = new FakeAuthorizationContext();

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }
}
