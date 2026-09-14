using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Chamados.Domain;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>ResolverChamadoCommandHandler (modo TDD): EmAtendimento -> Resolvido, nota obrigatória.</summary>
public class ResolverChamadoCommandHandlerTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Agora = AbertoEm.AddHours(3);

    [Fact]
    public async Task Resolver_um_chamado_EmAtendimento_com_nota_deve_mover_para_Resolvido()
    {
        var chamado = Chamado.Abrir(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        chamado.Atribuir(Guid.NewGuid());
        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var handler = new ResolverChamadoCommandHandler(repository, new FixedTimeProvider(Agora));

        var command = new ResolverChamadoCommand(chamado.Id, "Reiniciei o serviço.", "chave-resolver-1");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(StatusChamado.Resolvido, chamado.Status);
        Assert.Equal("Reiniciei o serviço.", chamado.NotaResolucao);
        Assert.Equal(Agora, chamado.ResolvidoEm);
    }

    [Fact]
    public async Task Resolver_um_chamado_Aberto_deve_devolver_Result_Failure()
    {
        var chamado = Chamado.Abrir(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var handler = new ResolverChamadoCommandHandler(repository, new FixedTimeProvider(Agora));

        var command = new ResolverChamadoCommand(chamado.Id, "Nota qualquer.", "chave-resolver-2");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsFailure);
    }
}
