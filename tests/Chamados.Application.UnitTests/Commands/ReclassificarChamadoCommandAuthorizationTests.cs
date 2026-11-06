using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// ReclassificarChamadoCommand.IsAuthorizedAsync (arquitetura/12; M4 de achados.md): a regra
/// saiu do handler e roda no AuthorizationBehavior, antes da idempotência. Autorizado para o
/// técnico atribuído, ou para um técnico cuja equipe tem o chamado na fila (Aberto); negado
/// para qualquer outro, inclusive membro da equipe com o chamado já EmAtendimento por outro
/// técnico (EstaNaFilaDaEquipeDoUsuarioAsync = false).
/// </summary>
public class ReclassificarChamadoCommandAuthorizationTests
{
    private static ReclassificarChamadoCommand NovoCommand(Guid chamadoId) =>
        new(chamadoId, PrioridadeChamado.Alta, "chave-reclassificar");

    [Fact]
    public async Task Tecnico_atribuido_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var context = new FakeAuthorizationContext().ComTecnicoAtribuido(currentUser.UserId, chamadoId);

        var autorizado = await NovoCommand(chamadoId).IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Tecnico_da_equipe_com_o_chamado_na_fila_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var context = new FakeAuthorizationContext().ComChamadoNaFilaDaEquipe(currentUser.UserId, chamadoId);

        var autorizado = await NovoCommand(chamadoId).IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Membro_da_equipe_com_o_chamado_fora_da_fila_e_sem_atribuicao_nao_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        // Membro da equipe, mas o chamado já está EmAtendimento por outro técnico: não está na fila.
        var context = new FakeAuthorizationContext().ComMembroDaEquipeResponsavel(currentUser.UserId, chamadoId);

        var autorizado = await NovoCommand(chamadoId).IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }

    [Fact]
    public async Task Usuario_sem_nenhum_vinculo_nao_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var context = new FakeAuthorizationContext();

        var autorizado = await NovoCommand(chamadoId).IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }
}
