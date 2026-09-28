using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// FecharChamadoCommand.IsAuthorizedAsync (arquitetura/12): ator de sistema (job de
/// fechamento automático) OU o solicitante do chamado, conferido no estado persistido via
/// EhSolicitanteAsync, nunca num campo informado por quem chama (o Command não carrega mais
/// SolicitanteId).
/// </summary>
public class FecharChamadoCommandAuthorizationTests
{
    [Fact]
    public async Task O_proprio_solicitante_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = new FecharChamadoCommand(chamadoId, "chave-fechar");
        var currentUser = new FakeCurrentUser();
        var context = new FakeAuthorizationContext().ComSolicitante(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Ator_de_sistema_deve_ser_autorizado_mesmo_sem_ser_o_solicitante()
    {
        var command = new FecharChamadoCommand(Guid.NewGuid(), "chave-fechar");
        var currentUser = FakeCurrentUser.SistemaAutomatizado();
        var context = new FakeAuthorizationContext();

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Usuario_que_nao_e_o_solicitante_nem_ator_de_sistema_nao_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = new FecharChamadoCommand(chamadoId, "chave-fechar");
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var context = new FakeAuthorizationContext()
            .ComSolicitante(Guid.NewGuid(), chamadoId) // outro usuário é o solicitante
            .ComTecnicoAtribuido(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }
}
