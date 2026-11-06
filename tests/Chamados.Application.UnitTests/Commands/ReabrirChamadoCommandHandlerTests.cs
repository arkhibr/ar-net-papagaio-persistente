using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Chamados.Domain;
using SharedKernel;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>ReabrirChamadoCommandHandler (modo TDD): Fechado -> Aberto, só dentro de 5 dias corridos.</summary>
public class ReabrirChamadoCommandHandlerTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ResolvidoEm = AbertoEm.AddHours(3);
    private static readonly DateTimeOffset FechadoEm = ResolvidoEm.AddDays(1);

    private static Chamado ChamadoFechado(Guid solicitanteId)
    {
        var chamado = Chamado.Abrir(solicitanteId, Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        chamado.Atribuir(Guid.NewGuid());
        chamado.Resolver("Resolvido.", ResolvidoEm);
        chamado.Fechar(FechadoEm);
        return chamado;
    }

    [Fact]
    public async Task Reabrir_dentro_da_janela_de_5_dias_deve_mover_para_Aberto()
    {
        var solicitanteId = Guid.NewGuid();
        var chamado = ChamadoFechado(solicitanteId);
        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var agora = FechadoEm.AddDays(2);
        var handler = new ReabrirChamadoCommandHandler(repository, new FixedTimeProvider(agora));

        var command = new ReabrirChamadoCommand(chamado.Id, "chave-reabrir-1");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(StatusChamado.Aberto, chamado.Status);
        Assert.Null(chamado.TecnicoAtribuidoId); // volta para a fila sem técnico (A2 de achados.md)
    }

    [Fact]
    public async Task Reabrir_apos_a_janela_de_5_dias_deve_devolver_Result_Failure()
    {
        var solicitanteId = Guid.NewGuid();
        var chamado = ChamadoFechado(solicitanteId);
        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var agora = FechadoEm.AddDays(5).AddSeconds(1);
        var handler = new ReabrirChamadoCommandHandler(repository, new FixedTimeProvider(agora));

        var command = new ReabrirChamadoCommand(chamado.Id, "chave-reabrir-2");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorKind.BusinessRule, resultado.ErrorKind);
        Assert.Equal(0, repository.Salvamentos);
    }

    [Fact]
    public async Task Chamado_inexistente_deve_devolver_Result_NotFound_sem_salvar()
    {
        var repository = new FakeChamadoRepository();
        var handler = new ReabrirChamadoCommandHandler(repository, new FixedTimeProvider(FechadoEm));

        var resultado = await handler.Handle(new ReabrirChamadoCommand(Guid.NewGuid(), "chave-reabrir-3"), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorKind.NotFound, resultado.ErrorKind);
        Assert.Equal(0, repository.Salvamentos);
    }
}
