using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// FecharChamadoCommand.IsAuthorizedAsync: solicitante OU ator de sistema (job de fechamento
/// automático após 3 dias, plano-de-arquitetura.md secao 3/5). Nenhum dos dois caminhos
/// consulta IAuthorizationContext.
/// </summary>
public class FecharChamadoCommandAuthorizationTests
{
    [Fact]
    public async Task O_proprio_solicitante_deve_ser_autorizado()
    {
        var solicitanteId = Guid.NewGuid();
        var command = new FecharChamadoCommand(Guid.NewGuid(), solicitanteId, "chave-fechar");
        var currentUser = new FakeCurrentUser { UserId = solicitanteId };
        var context = new FakeAuthorizationContext();

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Ator_de_sistema_deve_ser_autorizado_mesmo_sem_ser_o_solicitante()
    {
        var solicitanteId = Guid.NewGuid();
        var command = new FecharChamadoCommand(Guid.NewGuid(), solicitanteId, "chave-fechar");
        var currentUser = FakeCurrentUser.SistemaAutomatizado();
        var context = new FakeAuthorizationContext();

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Usuario_que_nao_e_o_solicitante_nem_ator_de_sistema_nao_deve_ser_autorizado()
    {
        var solicitanteId = Guid.NewGuid();
        var command = new FecharChamadoCommand(Guid.NewGuid(), solicitanteId, "chave-fechar");
        var currentUser = new FakeCurrentUser(); // UserId aleatório, diferente do solicitante
        var context = new FakeAuthorizationContext();

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }
}
