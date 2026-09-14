using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Chamados.Domain;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>DevolverChamadoCommandHandler (modo TDD): EmAtendimento -> Aberto.</summary>
public class DevolverChamadoCommandHandlerTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Devolver_um_chamado_EmAtendimento_deve_mover_para_Aberto()
    {
        var chamado = Chamado.Abrir(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        chamado.Atribuir(Guid.NewGuid());
        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var handler = new DevolverChamadoCommandHandler(repository);

        var command = new DevolverChamadoCommand(chamado.Id, "chave-devolver-1");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(StatusChamado.Aberto, chamado.Status);
    }

    [Fact]
    public async Task Devolver_um_chamado_Aberto_deve_devolver_Result_Failure()
    {
        var chamado = Chamado.Abrir(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var handler = new DevolverChamadoCommandHandler(repository);

        var command = new DevolverChamadoCommand(chamado.Id, "chave-devolver-2");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsFailure);
    }
}
