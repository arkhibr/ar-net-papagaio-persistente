using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Chamados.Domain;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>FecharChamadoCommandHandler (modo TDD): Resolvido -> Fechado.</summary>
public class FecharChamadoCommandHandlerTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ResolvidoEm = AbertoEm.AddHours(3);
    private static readonly DateTimeOffset Agora = ResolvidoEm.AddDays(1);

    private static Chamado ChamadoResolvido(Guid solicitanteId)
    {
        var chamado = Chamado.Abrir(solicitanteId, Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        chamado.Atribuir(Guid.NewGuid());
        chamado.Resolver("Resolvido.", ResolvidoEm);
        return chamado;
    }

    [Fact]
    public async Task Fechar_um_chamado_Resolvido_deve_mover_para_Fechado()
    {
        var solicitanteId = Guid.NewGuid();
        var chamado = ChamadoResolvido(solicitanteId);
        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var handler = new FecharChamadoCommandHandler(repository, new FixedTimeProvider(Agora));

        var command = new FecharChamadoCommand(chamado.Id, solicitanteId, "chave-fechar-1");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(StatusChamado.Fechado, chamado.Status);
        Assert.Equal(Agora, chamado.FechadoEm);
    }

    [Fact]
    public async Task Fechar_um_chamado_Aberto_deve_devolver_Result_Failure()
    {
        var solicitanteId = Guid.NewGuid();
        var chamado = Chamado.Abrir(solicitanteId, Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var handler = new FecharChamadoCommandHandler(repository, new FixedTimeProvider(Agora));

        var command = new FecharChamadoCommand(chamado.Id, solicitanteId, "chave-fechar-2");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsFailure);
    }
}
