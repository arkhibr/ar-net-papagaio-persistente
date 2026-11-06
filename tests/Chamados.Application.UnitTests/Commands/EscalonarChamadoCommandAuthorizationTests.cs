using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// EscalonarChamadoCommand.IsAuthorizedAsync: só ator de sistema (job de SLA no Worker, sem
/// rota HTTP). Nenhum usuário humano, mesmo Supervisor ou técnico atribuído, é autorizado.
/// </summary>
public class EscalonarChamadoCommandAuthorizationTests
{
    [Fact]
    public async Task Ator_de_sistema_deve_ser_autorizado()
    {
        var command = new EscalonarChamadoCommand(Guid.NewGuid(), "chave-escalonar-ciclo-1");
        var currentUser = FakeCurrentUser.SistemaAutomatizado();
        var context = new FakeAuthorizationContext();

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Usuario_humano_mesmo_com_papel_Supervisor_e_vinculo_nao_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = new EscalonarChamadoCommand(chamadoId, "chave-escalonar-ciclo-1");
        var currentUser = new FakeCurrentUser().ComPapel("Supervisor");
        var context = new FakeAuthorizationContext()
            .ComTecnicoAtribuido(currentUser.UserId, chamadoId)
            .ComMembroDaEquipeResponsavel(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }
}
