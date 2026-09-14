using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// EscalonarChamadoCommand.IsAuthorizedAsync: só ator de sistema (job de SLA no Worker,
/// nunca exposto via API pública). Nenhum usuário humano, mesmo com papel de
/// Técnico/Supervisor, deve ser autorizado.
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
    public async Task Usuario_humano_mesmo_com_papel_Supervisor_nao_deve_ser_autorizado()
    {
        var command = new EscalonarChamadoCommand(Guid.NewGuid(), "chave-escalonar-ciclo-1");
        var currentUser = new FakeCurrentUser().ComPapel("Supervisor");
        var context = new FakeAuthorizationContext();

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }
}
