using Catalogo.Contracts;
using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Mediator;
using Microsoft.Extensions.Options;
using SharedKernel;
using SharedKernel.Messaging;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// IdempotencyBehavior + UnitOfWorkBehavior encadeados na frente de um Command real do módulo
/// (AbrirChamadoCommand), não a mensagem sintética de SharedKernel.UnitTests
/// (13-estrategia-de-testes.md; arquitetura/06 e arquitetura/25). Cobre:
///   - reenvio com a mesma Idempotency-Key após sucesso não reexecuta o handler;
///   - Result.Failure (falha de negócio determinística) fica gravada e é devolvida no reenvio;
///   - exceção transitória vinda do IUnitOfWork propaga, libera a reserva e não conclui a chave,
///     de modo que uma nova tentativa executa o handler de novo.
/// </summary>
public class AbrirChamadoCommandIdempotencyBehaviorTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    private sealed class Pipeline
    {
        private readonly AbrirChamadoCommandHandler _handler;

        public Pipeline(FakeSender sender)
        {
            var timeProvider = new FixedTimeProvider(Agora);
            Repository = new FakeChamadoRepository();
            Store = new FakeIdempotencyStore(timeProvider);
            UnitOfWork = new FakeUnitOfWork(Store);
            _handler = new AbrirChamadoCommandHandler(Repository, sender, timeProvider);

            var pending = new PendingIdempotency();
            Idempotency = new IdempotencyBehavior<AbrirChamadoCommand, Result<Guid>>(
                new FixedModuleService<IIdempotencyStore>(Store),
                new FakeCurrentUser(),
                pending,
                timeProvider,
                Options.Create(new IdempotencyOptions()));
            UnitOfWorkBehavior = new UnitOfWorkBehavior<AbrirChamadoCommand, Result<Guid>>(
                new FixedModuleService<IUnitOfWork>(UnitOfWork), pending);
        }

        public FakeChamadoRepository Repository { get; }
        public FakeIdempotencyStore Store { get; }
        public FakeUnitOfWork UnitOfWork { get; }
        public IdempotencyBehavior<AbrirChamadoCommand, Result<Guid>> Idempotency { get; }
        public UnitOfWorkBehavior<AbrirChamadoCommand, Result<Guid>> UnitOfWorkBehavior { get; }
        public int ExecucoesDoHandler { get; private set; }

        public ValueTask<Result<Guid>> Enviar(AbrirChamadoCommand command) =>
            Idempotency.Handle(
                command,
                (msg, ct) => UnitOfWorkBehavior.Handle(msg, (m, c) =>
                {
                    ExecucoesDoHandler++;
                    return _handler.Handle(m, c);
                }, ct),
                CancellationToken.None);
    }

    [Fact]
    public async Task Segunda_chamada_com_a_mesma_Idempotency_Key_apos_sucesso_nao_deve_abrir_outro_chamado()
    {
        var categoriaId = Guid.NewGuid();
        var sender = new FakeSender().ComResposta(categoriaId, PrioridadeServico.Media, Guid.NewGuid(), horasDeSla: 24);
        var pipeline = new Pipeline(sender);
        var command = new AbrirChamadoCommand(Guid.NewGuid(), categoriaId, PrioridadeChamado.Media, "chave-abrir-idempotente-1");

        var primeiraResposta = await pipeline.Enviar(command);
        var segundaResposta = await pipeline.Enviar(command);

        Assert.True(primeiraResposta.IsSuccess);
        Assert.True(segundaResposta.IsSuccess);
        Assert.Equal(primeiraResposta.Value, segundaResposta.Value);
        Assert.Equal(1, pipeline.ExecucoesDoHandler);
        Assert.Single(pipeline.Repository.Todos);
        Assert.Equal(1, pipeline.UnitOfWork.Commits);
    }

    [Fact]
    public async Task Falha_de_negocio_deterministica_deve_ser_gravada_e_devolvida_no_reenvio_sem_reexecutar_o_handler()
    {
        var pipeline = new Pipeline(new FakeSender()); // Catálogo não resolve a categoria -> Result.Failure
        var command = new AbrirChamadoCommand(Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, "chave-abrir-idempotente-2");

        var primeiraResposta = await pipeline.Enviar(command);
        var segundaResposta = await pipeline.Enviar(command);

        Assert.True(primeiraResposta.IsFailure);
        Assert.True(segundaResposta.IsFailure);
        Assert.Equal(primeiraResposta.Error, segundaResposta.Error);
        Assert.Equal(primeiraResposta.ErrorKind, segundaResposta.ErrorKind);
        Assert.Equal(1, pipeline.ExecucoesDoHandler);

        // O que o handler encenou é descartado; só a conclusão da chave vai para o commit.
        Assert.Equal(1, pipeline.UnitOfWork.Descartes);
        Assert.Equal(1, pipeline.UnitOfWork.Commits);
        Assert.True(Assert.Single(pipeline.Store.Registros).Value.IsCompleted);
    }

    [Fact]
    public async Task Excecao_transitoria_do_UnitOfWork_deve_propagar_sem_concluir_a_chave_e_permitir_nova_tentativa()
    {
        var categoriaId = Guid.NewGuid();
        var sender = new FakeSender().ComResposta(categoriaId, PrioridadeServico.Media, Guid.NewGuid(), horasDeSla: 24);
        var pipeline = new Pipeline(sender);
        pipeline.UnitOfWork.FalharNoProximoSaveCom(new ConcurrencyException("Conflito de gravação otimista."));
        var command = new AbrirChamadoCommand(Guid.NewGuid(), categoriaId, PrioridadeChamado.Media, "chave-abrir-idempotente-3");

        await Assert.ThrowsAsync<ConcurrencyException>(() => pipeline.Enviar(command).AsTask());

        // A exceção nunca vira Result.Failure cacheado: a reserva foi liberada, nada concluído.
        Assert.Equal(1, pipeline.Store.Liberacoes);
        Assert.Empty(pipeline.Store.Registros);
        Assert.Equal(0, pipeline.UnitOfWork.Commits);

        var novaTentativa = await pipeline.Enviar(command);

        Assert.True(novaTentativa.IsSuccess);
        Assert.Equal(2, pipeline.ExecucoesDoHandler);
        Assert.True(Assert.Single(pipeline.Store.Registros).Value.IsCompleted);
    }
}
