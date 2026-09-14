using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Chamados.Domain;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>EscalonarChamadoCommandHandler (modo TDD): marca a flag Escalonado + DataEscalonamento.</summary>
public class EscalonarChamadoCommandHandlerTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Escalonar_um_chamado_Aberto_deve_marcar_a_flag_Escalonado()
    {
        var chamado = Chamado.Abrir(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Critica, horasDeSla: 4, AbertoEm);
        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var agora = AbertoEm.AddHours(5);
        var handler = new EscalonarChamadoCommandHandler(repository, new FixedTimeProvider(agora));

        var command = new EscalonarChamadoCommand(chamado.Id, "chave-escalonar-ciclo-1");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.True(chamado.Escalonado);
        Assert.Equal(agora, chamado.DataEscalonamento);
    }

    [Fact]
    public async Task Escalonar_um_chamado_ja_Resolvido_deve_devolver_Result_Failure()
    {
        var chamado = Chamado.Abrir(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Critica, horasDeSla: 4, AbertoEm);
        chamado.Atribuir(Guid.NewGuid());
        chamado.Resolver("Resolvido.", AbertoEm.AddHours(1));
        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var handler = new EscalonarChamadoCommandHandler(repository, new FixedTimeProvider(AbertoEm.AddHours(5)));

        var command = new EscalonarChamadoCommand(chamado.Id, "chave-escalonar-ciclo-2");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsFailure);
    }
}
