using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// AtribuirChamadoCommand.IsAuthorizedAsync (leitura B, arquitetura/12-autorizacao-por-recurso.md):
/// papel Tecnico + TecnicoId igual ao usuário autenticado (autoatribuição: ninguém atribui o
/// chamado a outro técnico) + membro da equipe responsável pelo chamado, consultado via
/// IChamadosAuthorizationContext. Testado isoladamente do handler (13-estrategia-de-testes.md).
/// </summary>
public class AtribuirChamadoCommandAuthorizationTests
{
    private static AtribuirChamadoCommand NovoCommand(Guid chamadoId, Guid tecnicoId) =>
        new(chamadoId, tecnicoId, RowVersion: [1, 2, 3], IdempotencyKey: "chave-atribuir");

    [Fact]
    public async Task Tecnico_membro_da_equipe_se_autoatribuindo_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var command = NovoCommand(chamadoId, currentUser.UserId);
        var context = new FakeAuthorizationContext().ComMembroDaEquipeResponsavel(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Tecnico_membro_atribuindo_o_chamado_a_outro_tecnico_nao_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var command = NovoCommand(chamadoId, tecnicoId: Guid.NewGuid()); // TecnicoId != usuário atual
        var context = new FakeAuthorizationContext().ComMembroDaEquipeResponsavel(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }

    [Fact]
    public async Task Usuario_sem_papel_Tecnico_nunca_deve_ser_autorizado_mesmo_sendo_membro()
    {
        var chamadoId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser(); // sem papel Tecnico
        var command = NovoCommand(chamadoId, currentUser.UserId);
        var context = new FakeAuthorizationContext().ComMembroDaEquipeResponsavel(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }

    [Fact]
    public async Task Tecnico_que_nao_e_membro_da_equipe_responsavel_nao_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var command = NovoCommand(chamadoId, currentUser.UserId);
        var context = new FakeAuthorizationContext(); // sem vínculo nenhum configurado

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }
}
