using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// AtribuirChamadoCommand.IsAuthorizedAsync (leitura B, arquitetura/12-autorizacao-por-recurso.md):
/// papel Tecnico + vínculo com o chamado (o vínculo técnico-equipe responsável é dado do
/// módulo Catalogo, consultado via IAuthorizationContext na implementação real de
/// Infrastructure — plano-de-arquitetura.md secao 5, nota). Testado isoladamente do handler,
/// só a checagem de autorização (13-estrategia-de-testes.md).
/// </summary>
public class AtribuirChamadoCommandAuthorizationTests
{
    private static AtribuirChamadoCommand NovoCommand(Guid chamadoId) =>
        new(chamadoId, TecnicoId: Guid.NewGuid(), RowVersion: [1, 2, 3], IdempotencyKey: "chave-atribuir");

    [Fact]
    public async Task Tecnico_com_vinculo_ao_chamado_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = NovoCommand(chamadoId);
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var context = new FakeAuthorizationContext().ComVinculo(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Usuario_sem_papel_Tecnico_nunca_deve_ser_autorizado_mesmo_com_vinculo()
    {
        var chamadoId = Guid.NewGuid();
        var command = NovoCommand(chamadoId);
        var currentUser = new FakeCurrentUser(); // sem papel Tecnico
        var context = new FakeAuthorizationContext().ComVinculo(currentUser.UserId, chamadoId);

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }

    [Fact]
    public async Task Tecnico_sem_vinculo_com_o_chamado_nao_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var command = NovoCommand(chamadoId);
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var context = new FakeAuthorizationContext(); // sem vínculo nenhum configurado

        var autorizado = await command.IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }
}
