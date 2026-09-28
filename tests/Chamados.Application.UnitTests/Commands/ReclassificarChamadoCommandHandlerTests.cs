using Catalogo.Contracts;
using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Chamados.Domain;
using SharedKernel;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// ReclassificarChamadoCommandHandler: reclassifica a prioridade e recalcula o prazo a partir
/// da abertura com o SLA resolvido pelo Catálogo (ResolverEquipeESlaQuery, arquitetura/29).
/// A autorização (técnico atribuído ou chamado na fila da equipe do usuário) saiu do handler e
/// roda no pipeline (M4 de achados.md): coberta em ReclassificarChamadoCommandAuthorizationTests.
/// </summary>
public class ReclassificarChamadoCommandHandlerTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reclassificar_deve_aplicar_o_SLA_da_nova_prioridade_a_partir_da_abertura()
    {
        var categoriaId = Guid.NewGuid();
        var equipeId = Guid.NewGuid();
        var chamado = Chamado.Abrir(Guid.NewGuid(), categoriaId, equipeId, PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        chamado.Atribuir(Guid.NewGuid());

        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var sender = new FakeSender().ComResposta(categoriaId, PrioridadeServico.Critica, equipeId, horasDeSla: 4);
        var handler = new ReclassificarChamadoCommandHandler(repository, sender);

        var resultado = await handler.Handle(
            new ReclassificarChamadoCommand(chamado.Id, PrioridadeChamado.Critica, "chave-reclassificar-1"),
            CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(PrioridadeChamado.Critica, chamado.Prioridade);
        Assert.Equal(AbertoEm.AddHours(4), chamado.PrazoSla);
        Assert.Equal(1, repository.Salvamentos);
    }

    [Fact]
    public async Task Catalogo_sem_SLA_para_a_nova_prioridade_deve_devolver_Result_Failure_sem_alterar_o_chamado()
    {
        var categoriaId = Guid.NewGuid();
        var chamado = Chamado.Abrir(Guid.NewGuid(), categoriaId, Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        var prazoOriginal = chamado.PrazoSla;

        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var sender = new FakeSender(); // Catálogo não resolve nada -> Failure
        var handler = new ReclassificarChamadoCommandHandler(repository, sender);

        var resultado = await handler.Handle(
            new ReclassificarChamadoCommand(chamado.Id, PrioridadeChamado.Alta, "chave-reclassificar-2"),
            CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorKind.BusinessRule, resultado.ErrorKind);
        Assert.Equal(PrioridadeChamado.Media, chamado.Prioridade);
        Assert.Equal(prazoOriginal, chamado.PrazoSla);
        Assert.Equal(0, repository.Salvamentos);
    }

    [Fact]
    public async Task Reclassificar_um_chamado_Resolvido_deve_devolver_Result_Failure_sem_deixar_a_DomainException_escapar()
    {
        var categoriaId = Guid.NewGuid();
        var equipeId = Guid.NewGuid();
        var chamado = Chamado.Abrir(Guid.NewGuid(), categoriaId, equipeId, PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        chamado.Atribuir(Guid.NewGuid());
        chamado.Resolver("Resolvido.", AbertoEm.AddHours(1));

        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var sender = new FakeSender().ComResposta(categoriaId, PrioridadeServico.Alta, equipeId, horasDeSla: 8);
        var handler = new ReclassificarChamadoCommandHandler(repository, sender);

        var resultado = await handler.Handle(
            new ReclassificarChamadoCommand(chamado.Id, PrioridadeChamado.Alta, "chave-reclassificar-3"),
            CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorKind.BusinessRule, resultado.ErrorKind);
        Assert.Equal(0, repository.Salvamentos);
    }

    [Fact]
    public async Task Chamado_inexistente_deve_devolver_Result_NotFound_sem_salvar()
    {
        var repository = new FakeChamadoRepository();
        var handler = new ReclassificarChamadoCommandHandler(repository, new FakeSender());

        var resultado = await handler.Handle(
            new ReclassificarChamadoCommand(Guid.NewGuid(), PrioridadeChamado.Alta, "chave-reclassificar-4"),
            CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorKind.NotFound, resultado.ErrorKind);
        Assert.Equal(0, repository.Salvamentos);
    }
}
