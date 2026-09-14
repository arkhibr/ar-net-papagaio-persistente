using Chamados.Application.Queries;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Chamados.Domain;
using Xunit;

namespace Chamados.Application.UnitTests.Queries;

/// <summary>
/// FilaDaEquipeQueryHandler: filtro por linha (equipe do próprio ator, nunca aceita EquipeId
/// vindo de fora). A checagem de papel Técnico/Supervisor NÃO é responsabilidade do handler —
/// é [Authorize(Roles=...)] nativo no EquipesController (leitura A, arquitetura/12), coberta por
/// Api.IntegrationTests, não aqui. Este handler cobre o filtro por linha: chamado da própria
/// equipe visível, ator sem equipe vinculada devolve Failure.
/// </summary>
public class FilaDaEquipeQueryHandlerTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Tecnico_deve_ver_somente_os_chamados_da_propria_equipe()
    {
        var equipeDoTecnico = Guid.NewGuid();
        var outraEquipe = Guid.NewGuid();
        var tecnicoId = Guid.NewGuid();

        var chamadoDaEquipe = Chamado.Abrir(
            Guid.NewGuid(), Guid.NewGuid(), equipeDoTecnico, PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        var chamadoDeOutraEquipe = Chamado.Abrir(
            Guid.NewGuid(), Guid.NewGuid(), outraEquipe, PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);

        var repository = new FakeChamadoRepository().ComChamado(chamadoDaEquipe).ComChamado(chamadoDeOutraEquipe);
        var equipeResolver = new FakeEquipeDoUsuarioResolver().ComEquipe(tecnicoId, equipeDoTecnico);
        var currentUser = new FakeCurrentUser { UserId = tecnicoId }.ComPapel("Tecnico");
        var handler = new FilaDaEquipeQueryHandler(repository, equipeResolver, currentUser);

        var resultado = await handler.Handle(new FilaDaEquipeQuery(Page: 1, PageSize: 20), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var chamado = Assert.Single(resultado.Value!);
        Assert.Equal(chamadoDaEquipe.Id, chamado.Id);
    }

    [Fact]
    public async Task Ator_sem_equipe_vinculada_deve_devolver_Result_Failure()
    {
        var tecnicoId = Guid.NewGuid();
        var repository = new FakeChamadoRepository();
        var equipeResolver = new FakeEquipeDoUsuarioResolver(); // sem vínculo para nenhum ator
        var currentUser = new FakeCurrentUser { UserId = tecnicoId }.ComPapel("Tecnico");
        var handler = new FilaDaEquipeQueryHandler(repository, equipeResolver, currentUser);

        var resultado = await handler.Handle(new FilaDaEquipeQuery(Page: 1, PageSize: 20), CancellationToken.None);

        Assert.True(resultado.IsFailure);
    }
}
