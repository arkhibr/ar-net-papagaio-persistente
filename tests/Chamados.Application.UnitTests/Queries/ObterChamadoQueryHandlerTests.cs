using Chamados.Application.Queries;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Chamados.Domain;
using SharedKernel;
using Xunit;

namespace Chamados.Application.UnitTests.Queries;

/// <summary>
/// ObterChamadoQueryHandler: guard explícito de autorização (ver ObterChamadoQuery) —
/// solicitante dono, técnico atribuído, ou membro (Tecnico/Supervisor) da equipe responsável,
/// em qualquer status. Cobre os três caminhos autorizados, o caminho negado
/// (AuthorizationDeniedException) e "chamado não encontrado" (Result.Failure).
/// </summary>
public class ObterChamadoQueryHandlerTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Solicitante_dono_deve_ver_o_proprio_chamado()
    {
        var solicitanteId = Guid.NewGuid();
        var chamado = Chamado.Abrir(
            solicitanteId, Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);

        var repository = new FakeChamadoRepository().ComChamado(chamado).ComRowVersion(chamado.Id, [9, 9, 9]);
        var equipeMembershipChecker = new FakeEquipeMembershipChecker();
        var currentUser = new FakeCurrentUser { UserId = solicitanteId };
        var handler = new ObterChamadoQueryHandler(repository, equipeMembershipChecker, currentUser);

        var resultado = await handler.Handle(new ObterChamadoQuery(chamado.Id), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(chamado.Id, resultado.Value!.Id);
        Assert.Equal(solicitanteId, resultado.Value!.SolicitanteId);
        // RowVersion vai em base64 no DTO — a Api usa isso pra montar o header ETag sem
        // reinterpretar o valor (arquitetura/06).
        Assert.Equal(Convert.ToBase64String([9, 9, 9]), resultado.Value!.RowVersion);
    }

    [Fact]
    public async Task Tecnico_atribuido_deve_ver_o_chamado()
    {
        var tecnicoId = Guid.NewGuid();
        var equipeId = Guid.NewGuid();
        var chamado = Chamado.Abrir(
            Guid.NewGuid(), Guid.NewGuid(), equipeId, PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        chamado.Atribuir(tecnicoId);

        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var equipeMembershipChecker = new FakeEquipeMembershipChecker();
        var currentUser = new FakeCurrentUser { UserId = tecnicoId }.ComPapel("Tecnico");
        var handler = new ObterChamadoQueryHandler(repository, equipeMembershipChecker, currentUser);

        var resultado = await handler.Handle(new ObterChamadoQuery(chamado.Id), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(tecnicoId, resultado.Value!.TecnicoAtribuidoId);
    }

    [Fact]
    public async Task Membro_da_equipe_responsavel_deve_ver_o_chamado_mesmo_sem_estar_atribuido()
    {
        var equipeId = Guid.NewGuid();
        var membroId = Guid.NewGuid();
        var chamado = Chamado.Abrir(
            Guid.NewGuid(), Guid.NewGuid(), equipeId, PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        chamado.Atribuir(Guid.NewGuid()); // atribuído a outro técnico, ainda EmAtendimento

        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var equipeMembershipChecker = new FakeEquipeMembershipChecker().ComMembro(membroId, equipeId);
        var currentUser = new FakeCurrentUser { UserId = membroId }.ComPapel("Supervisor");
        var handler = new ObterChamadoQueryHandler(repository, equipeMembershipChecker, currentUser);

        var resultado = await handler.Handle(new ObterChamadoQuery(chamado.Id), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Usuario_sem_nenhum_vinculo_deve_lancar_AuthorizationDeniedException()
    {
        var chamado = Chamado.Abrir(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);

        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var equipeMembershipChecker = new FakeEquipeMembershipChecker();
        var currentUser = new FakeCurrentUser(); // sem vínculo, sem papel
        var handler = new ObterChamadoQueryHandler(repository, equipeMembershipChecker, currentUser);

        await Assert.ThrowsAsync<AuthorizationDeniedException>(
            () => handler.Handle(new ObterChamadoQuery(chamado.Id), CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Chamado_inexistente_deve_devolver_Result_Failure()
    {
        var repository = new FakeChamadoRepository();
        var equipeMembershipChecker = new FakeEquipeMembershipChecker();
        var currentUser = new FakeCurrentUser();
        var handler = new ObterChamadoQueryHandler(repository, equipeMembershipChecker, currentUser);

        var resultado = await handler.Handle(new ObterChamadoQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(resultado.IsFailure);
    }
}
