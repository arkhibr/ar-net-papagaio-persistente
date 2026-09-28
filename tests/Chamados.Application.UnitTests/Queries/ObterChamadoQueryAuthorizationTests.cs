using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using SharedKernel;
using Xunit;

namespace Chamados.Application.UnitTests.Queries;

/// <summary>
/// ObterChamadoQuery.IsAuthorizedAsync (arquitetura/12; M3 de achados.md): a autorização saiu
/// do handler e roda no pipeline. Visível para o solicitante, o técnico atribuído e
/// técnicos/supervisores membros da equipe responsável; membro sem papel é negado. A negação
/// esconde a existência do recurso (HideExistenceWhenDenied = true, vira 404 no behavior).
/// </summary>
public class ObterChamadoQueryAuthorizationTests
{
    [Fact]
    public async Task Solicitante_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser();
        var context = new FakeAuthorizationContext().ComSolicitante(currentUser.UserId, chamadoId);

        var autorizado = await new ObterChamadoQuery(chamadoId).IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Tecnico_atribuido_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser().ComPapel("Tecnico");
        var context = new FakeAuthorizationContext().ComTecnicoAtribuido(currentUser.UserId, chamadoId);

        var autorizado = await new ObterChamadoQuery(chamadoId).IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Theory]
    [InlineData("Tecnico")]
    [InlineData("Supervisor")]
    public async Task Tecnico_ou_Supervisor_membro_da_equipe_responsavel_deve_ser_autorizado(string papel)
    {
        var chamadoId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser().ComPapel(papel);
        var context = new FakeAuthorizationContext().ComMembroDaEquipeResponsavel(currentUser.UserId, chamadoId);

        var autorizado = await new ObterChamadoQuery(chamadoId).IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.True(autorizado);
    }

    [Fact]
    public async Task Membro_da_equipe_sem_papel_Tecnico_ou_Supervisor_nao_deve_ser_autorizado()
    {
        var chamadoId = Guid.NewGuid();
        var currentUser = new FakeCurrentUser(); // sem papel
        var context = new FakeAuthorizationContext().ComMembroDaEquipeResponsavel(currentUser.UserId, chamadoId);

        var autorizado = await new ObterChamadoQuery(chamadoId).IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }

    [Fact]
    public async Task Usuario_sem_nenhum_vinculo_nao_deve_ser_autorizado()
    {
        var currentUser = new FakeCurrentUser().ComPapel("Supervisor");
        var context = new FakeAuthorizationContext();

        var autorizado = await new ObterChamadoQuery(Guid.NewGuid()).IsAuthorizedAsync(currentUser, context, CancellationToken.None);

        Assert.False(autorizado);
    }

    [Fact]
    public void Negacao_deve_esconder_a_existencia_do_chamado()
    {
        IRequiresAuthorization query = new ObterChamadoQuery(Guid.NewGuid());

        Assert.True(query.HideExistenceWhenDenied);
    }
}
