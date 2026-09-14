using Catalogo.Contracts;
using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Mediator;
using SharedKernel;
using SharedKernel.Messaging;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// IdempotencyBehavior encadeado na frente de um Command real do módulo (AbrirChamadoCommand,
/// que implementa IIdempotentCommand) — não a mensagem sintética de
/// SharedKernel.UnitTests/IdempotencyBehaviorTests. Cobre o caso obrigatório de
/// 13-estrategia-de-testes.md: segunda chamada com a mesma Idempotency-Key após sucesso não
/// reexecuta o handler de negócio, usando os fakes já existentes deste módulo
/// (FakeChamadoRepository, FakeSender, FixedTimeProvider, FakeIdempotencyStore).
/// </summary>
public class AbrirChamadoCommandIdempotencyBehaviorTests
{
    [Fact]
    public async Task Segunda_chamada_com_a_mesma_Idempotency_Key_apos_sucesso_nao_deve_abrir_outro_chamado()
    {
        var categoriaId = Guid.NewGuid();
        var equipeId = Guid.NewGuid();
        var solicitanteId = Guid.NewGuid();
        var agora = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

        var repository = new FakeChamadoRepository();
        var sender = new FakeSender().ComResposta(categoriaId, PrioridadeServico.Media, equipeId, horasDeSla: 24);
        var timeProvider = new FixedTimeProvider(agora);
        var handler = new AbrirChamadoCommandHandler(repository, sender, timeProvider);
        var idempotencyStore = new FakeIdempotencyStore();
        var behavior = new IdempotencyBehavior<AbrirChamadoCommand, Result<Guid>>(idempotencyStore);

        var command = new AbrirChamadoCommand(solicitanteId, categoriaId, PrioridadeChamado.Media, "chave-abrir-idempotente-1");

        MessageHandlerDelegate<AbrirChamadoCommand, Result<Guid>> next =
            (msg, ct) => handler.Handle(msg, ct);

        var primeiraResposta = await behavior.Handle(command, next, CancellationToken.None);
        var segundaResposta = await behavior.Handle(command, next, CancellationToken.None);

        Assert.True(primeiraResposta.IsSuccess);
        Assert.True(segundaResposta.IsSuccess);
        Assert.Equal(primeiraResposta.Value, segundaResposta.Value);

        // O handler de negócio só deve ter criado um chamado — a segunda chamada devolveu a
        // resposta cacheada pelo IdempotencyBehavior, não reexecutou AbrirChamadoCommandHandler.
        Assert.Single(repository.Todos);
    }
}
