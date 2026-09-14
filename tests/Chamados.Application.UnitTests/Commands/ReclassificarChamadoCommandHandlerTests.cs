using Catalogo.Contracts;
using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Chamados.Domain;
using SharedKernel;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// ReclassificarChamadoCommandHandler (modo TDD) — guard explícito no handler, NÃO
/// IRequiresAuthorization padrão (lacuna 1 do plano-de-arquitetura.md, secao 2/5): a
/// autorização depende do ESTADO do agregado (técnico atribuído, ou qualquer técnico da
/// equipe responsável se o chamado ainda está Aberto). Por isso os casos de autorização são
/// testados aqui, através do handler completo (com o Chamado carregado), e não como um
/// IsAuthorizedAsync isolado — esse Command nem implementa a interface.
///
/// Autorizado:
///   - o próprio técnico atribuído, em qualquer estado válido (Aberto ou EmAtendimento);
///   - qualquer técnico da equipe responsável, SE o chamado ainda está Aberto (sem técnico
///     atribuído ainda, ou o vínculo não importa nesse estado).
/// Negado:
///   - técnico que não é o atribuído E o chamado já está EmAtendimento;
///   - usuário que não é sequer membro da equipe responsável, em qualquer estado.
/// </summary>
public class ReclassificarChamadoCommandHandlerTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    private static (Guid CategoriaId, Guid EquipeId) NovaCategoriaEEquipe() => (Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task Tecnico_atribuido_deve_poder_reclassificar_um_chamado_EmAtendimento()
    {
        var (categoriaId, equipeId) = NovaCategoriaEEquipe();
        var chamado = Chamado.Abrir(
            Guid.NewGuid(), categoriaId, equipeId, PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        var tecnicoId = Guid.NewGuid();
        chamado.Atribuir(tecnicoId);

        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var membershipChecker = new FakeEquipeMembershipChecker();
        var currentUser = new FakeCurrentUser { UserId = tecnicoId };
        var sender = new FakeSender().ComResposta(categoriaId, PrioridadeServico.Critica, equipeId, horasDeSla: 4);
        var handler = new ReclassificarChamadoCommandHandler(repository, membershipChecker, currentUser, sender);

        var command = new ReclassificarChamadoCommand(chamado.Id, PrioridadeChamado.Critica, "chave-reclassificar-1");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(PrioridadeChamado.Critica, chamado.Prioridade);
        Assert.Equal(AbertoEm.AddHours(4), chamado.PrazoSla);
    }

    [Fact]
    public async Task Tecnico_da_equipe_nao_atribuido_deve_poder_reclassificar_enquanto_Aberto()
    {
        var (categoriaId, equipeId) = NovaCategoriaEEquipe();
        var chamado = Chamado.Abrir(
            Guid.NewGuid(), categoriaId, equipeId, PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);

        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var tecnicoDaEquipe = Guid.NewGuid();
        var membershipChecker = new FakeEquipeMembershipChecker().ComMembro(tecnicoDaEquipe, equipeId);
        var currentUser = new FakeCurrentUser { UserId = tecnicoDaEquipe };
        var sender = new FakeSender().ComResposta(categoriaId, PrioridadeServico.Alta, equipeId, horasDeSla: 8);
        var handler = new ReclassificarChamadoCommandHandler(repository, membershipChecker, currentUser, sender);

        var command = new ReclassificarChamadoCommand(chamado.Id, PrioridadeChamado.Alta, "chave-reclassificar-2");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(PrioridadeChamado.Alta, chamado.Prioridade);
    }

    [Fact]
    public async Task Tecnico_da_equipe_nao_atribuido_nao_deve_poder_reclassificar_um_chamado_ja_EmAtendimento_por_outro_tecnico()
    {
        var (categoriaId, equipeId) = NovaCategoriaEEquipe();
        var chamado = Chamado.Abrir(
            Guid.NewGuid(), categoriaId, equipeId, PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        chamado.Atribuir(Guid.NewGuid()); // outro técnico assume o chamado

        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var tecnicoDaEquipe = Guid.NewGuid();
        var membershipChecker = new FakeEquipeMembershipChecker().ComMembro(tecnicoDaEquipe, equipeId);
        var currentUser = new FakeCurrentUser { UserId = tecnicoDaEquipe };
        var sender = new FakeSender().ComResposta(categoriaId, PrioridadeServico.Alta, equipeId, horasDeSla: 8);
        var handler = new ReclassificarChamadoCommandHandler(repository, membershipChecker, currentUser, sender);

        var command = new ReclassificarChamadoCommand(chamado.Id, PrioridadeChamado.Alta, "chave-reclassificar-3");

        await Assert.ThrowsAsync<AuthorizationDeniedException>(
            () => handler.Handle(command, CancellationToken.None).AsTask());

        // Estado do agregado não deve ter sido alterado por uma tentativa não autorizada.
        Assert.Equal(PrioridadeChamado.Media, chamado.Prioridade);
    }

    [Fact]
    public async Task Usuario_que_nao_e_membro_da_equipe_responsavel_nunca_deve_poder_reclassificar()
    {
        var (categoriaId, equipeId) = NovaCategoriaEEquipe();
        var chamado = Chamado.Abrir(
            Guid.NewGuid(), categoriaId, equipeId, PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);

        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var membershipChecker = new FakeEquipeMembershipChecker(); // ninguém é membro
        var currentUser = new FakeCurrentUser();
        var sender = new FakeSender().ComResposta(categoriaId, PrioridadeServico.Alta, equipeId, horasDeSla: 8);
        var handler = new ReclassificarChamadoCommandHandler(repository, membershipChecker, currentUser, sender);

        var command = new ReclassificarChamadoCommand(chamado.Id, PrioridadeChamado.Alta, "chave-reclassificar-4");

        await Assert.ThrowsAsync<AuthorizationDeniedException>(
            () => handler.Handle(command, CancellationToken.None).AsTask());
    }
}
