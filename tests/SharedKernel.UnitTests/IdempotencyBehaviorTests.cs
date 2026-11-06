using Mediator;
using Microsoft.Extensions.Options;
using SharedKernel.Messaging;
using Xunit;

namespace SharedKernel.UnitTests;

/// <summary>
/// IdempotencyBehavior + UnitOfWorkBehavior encadeados como no pipeline real
/// (arquitetura/06, arquitetura/25; C1 e C2 de achados.md). Um "banco" fake separa o que foi só
/// encenado do que foi gravado, e registra em qual commit cada coisa entrou.
/// </summary>
public class IdempotencyBehaviorTests
{
    private sealed record Mensagem(string IdempotencyKey, int Valor)
        : IRequest<Result<Guid>>, IIdempotentCommand, ITransactionalCommand;

    private sealed class FakeBanco
    {
        public List<string> NegocioPendente { get; } = [];
        public List<string> NegocioGravado { get; } = [];
        public Dictionary<string, IdempotencyRecord> Registros { get; } = new();
        public Dictionary<string, string> ConclusoesPendentes { get; } = new();
        public List<(int Negocio, int Conclusoes)> Commits { get; } = [];
    }

    private sealed class FakeStore : IIdempotencyStore
    {
        private readonly FakeBanco _banco;
        private readonly TimeProvider _tempo;

        public FakeStore(FakeBanco banco, TimeProvider tempo)
        {
            _banco = banco;
            _tempo = tempo;
        }

        private static string K(IdempotencyRequest r) => $"{r.Scope}|{r.Key}";

        public Task<IdempotencyRecord?> FindAsync(IdempotencyRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(_banco.Registros.GetValueOrDefault(K(request)));

        public Task ReserveAsync(IdempotencyRequest request, CancellationToken cancellationToken)
        {
            if (!_banco.Registros.TryAdd(K(request), new IdempotencyRecord(false, request.PayloadHash, null, _tempo.GetUtcNow())))
            {
                throw new OperationInProgressException("corrida");
            }

            return Task.CompletedTask;
        }

        public Task<bool> TryRenewReservationAsync(
            IdempotencyRequest request, DateTimeOffset previousReservedAt, CancellationToken cancellationToken)
        {
            var atual = _banco.Registros[K(request)];
            if (atual.IsCompleted || atual.ReservedAt != previousReservedAt)
            {
                return Task.FromResult(false);
            }

            _banco.Registros[K(request)] = atual with { ReservedAt = _tempo.GetUtcNow() };
            return Task.FromResult(true);
        }

        public Task StageCompletionAsync(IdempotencyRequest request, string serializedResponse, CancellationToken cancellationToken)
        {
            _banco.ConclusoesPendentes[K(request)] = serializedResponse;
            return Task.CompletedTask;
        }

        public Task CompleteAsync(IdempotencyRequest request, string serializedResponse, CancellationToken cancellationToken)
        {
            _banco.Registros[K(request)] = _banco.Registros[K(request)] with { IsCompleted = true, SerializedResponse = serializedResponse };
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(IdempotencyRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _banco.NegocioPendente.Clear();
            _banco.ConclusoesPendentes.Clear();
            _banco.Registros.Remove(K(request));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        private readonly FakeBanco _banco;

        public FakeUnitOfWork(FakeBanco banco)
        {
            _banco = banco;
        }

        public Exception? FalhaNoCommit { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            if (FalhaNoCommit is not null)
            {
                throw FalhaNoCommit;
            }

            _banco.Commits.Add((_banco.NegocioPendente.Count, _banco.ConclusoesPendentes.Count));
            _banco.NegocioGravado.AddRange(_banco.NegocioPendente);
            _banco.NegocioPendente.Clear();
            foreach (var (chave, resposta) in _banco.ConclusoesPendentes)
            {
                _banco.Registros[chave] = _banco.Registros[chave] with { IsCompleted = true, SerializedResponse = resposta };
            }

            _banco.ConclusoesPendentes.Clear();
            return Task.FromResult(1);
        }

        public void DiscardChanges()
        {
            _banco.NegocioPendente.Clear();
            _banco.ConclusoesPendentes.Clear();
        }
    }

    private sealed class Pipeline
    {
        public FakeBanco Banco { get; } = new();
        public FixedTimeProvider Tempo { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; }
        public int ChamadasAoHandler { get; private set; }
        public Func<Mensagem, Result<Guid>> Handler { get; set; } = _ => Result<Guid>.Success(Guid.NewGuid());

        private readonly FakeStore _store;

        public Pipeline()
        {
            _store = new FakeStore(Banco, Tempo);
            UnitOfWork = new FakeUnitOfWork(Banco);
        }

        public async Task<Result<Guid>> EnviarAsync(Mensagem mensagem, ICurrentUser ator, CancellationToken cancellationToken = default)
        {
            var pending = new PendingIdempotency();
            var idempotencia = new IdempotencyBehavior<Mensagem, Result<Guid>>(
                new FixedModuleService<IIdempotencyStore>(_store), ator, pending, Tempo, Options.Create(new IdempotencyOptions()));
            var unitOfWork = new UnitOfWorkBehavior<Mensagem, Result<Guid>>(
                new FixedModuleService<IUnitOfWork>(UnitOfWork), pending);

            return await idempotencia.Handle(
                mensagem,
                (m, ct) => unitOfWork.Handle(m, (m2, _) =>
                {
                    ChamadasAoHandler++;
                    Banco.NegocioPendente.Add($"efeito-{m2.Valor}");
                    return ValueTask.FromResult(Handler(m2));
                }, ct),
                cancellationToken);
        }
    }

    [Fact]
    public async Task Sucesso_grava_a_conclusao_da_chave_no_mesmo_commit_da_mudanca_de_negocio()
    {
        var pipeline = new Pipeline();

        await pipeline.EnviarAsync(new Mensagem("k", 1), new FakeCurrentUser());

        Assert.Equal([(1, 1)], pipeline.Banco.Commits);
        Assert.Single(pipeline.Banco.NegocioGravado);
        Assert.True(pipeline.Banco.Registros.Values.Single().IsCompleted);
    }

    [Fact]
    public async Task Reenvio_apos_sucesso_devolve_a_resposta_gravada_sem_chamar_o_handler()
    {
        var pipeline = new Pipeline();
        var ator = new FakeCurrentUser();

        var primeira = await pipeline.EnviarAsync(new Mensagem("k", 1), ator);
        var segunda = await pipeline.EnviarAsync(new Mensagem("k", 1), ator);

        Assert.Equal(1, pipeline.ChamadasAoHandler);
        Assert.Equal(primeira.Value, segunda.Value);
    }

    [Fact]
    public async Task Result_Failure_descarta_o_que_o_handler_encenou_e_grava_so_a_falha_na_chave()
    {
        var pipeline = new Pipeline { Handler = _ => Result<Guid>.Failure("Regra violada.") };
        var ator = new FakeCurrentUser();

        var primeira = await pipeline.EnviarAsync(new Mensagem("k", 1), ator);
        var segunda = await pipeline.EnviarAsync(new Mensagem("k", 1), ator);

        Assert.True(primeira.IsFailure);
        Assert.Equal("Regra violada.", segunda.Error);
        Assert.Equal(1, pipeline.ChamadasAoHandler);
        Assert.Empty(pipeline.Banco.NegocioGravado);
        Assert.Equal([(0, 1)], pipeline.Banco.Commits);
    }

    [Fact]
    public async Task Excecao_no_commit_nao_grava_nada_libera_a_chave_e_permite_nova_tentativa()
    {
        var pipeline = new Pipeline();
        var ator = new FakeCurrentUser();
        pipeline.UnitOfWork.FalhaNoCommit = new ConcurrencyException("conflito");

        await Assert.ThrowsAsync<ConcurrencyException>(() => pipeline.EnviarAsync(new Mensagem("k", 1), ator));

        Assert.Empty(pipeline.Banco.NegocioGravado);
        Assert.Empty(pipeline.Banco.Registros);

        pipeline.UnitOfWork.FalhaNoCommit = null;
        var nova = await pipeline.EnviarAsync(new Mensagem("k", 1), ator);

        Assert.True(nova.IsSuccess);
        Assert.Equal(2, pipeline.ChamadasAoHandler);
    }

    [Fact]
    public async Task Cancelamento_do_cliente_ainda_libera_a_reserva()
    {
        var pipeline = new Pipeline { Handler = _ => throw new OperationCanceledException() };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => pipeline.EnviarAsync(new Mensagem("k", 1), new FakeCurrentUser(), cts.Token));

        Assert.Empty(pipeline.Banco.Registros);
    }

    [Fact]
    public async Task Mesma_chave_de_outro_usuario_e_outra_requisicao()
    {
        var pipeline = new Pipeline();

        var deA = await pipeline.EnviarAsync(new Mensagem("k", 1), new FakeCurrentUser());
        var deB = await pipeline.EnviarAsync(new Mensagem("k", 1), new FakeCurrentUser());

        Assert.Equal(2, pipeline.ChamadasAoHandler);
        Assert.NotEqual(deA.Value, deB.Value);
    }

    [Fact]
    public async Task Mesma_chave_do_mesmo_usuario_com_payload_diferente_lanca_IdempotencyKeyReusedException()
    {
        var pipeline = new Pipeline();
        var ator = new FakeCurrentUser();
        await pipeline.EnviarAsync(new Mensagem("k", 1), ator);

        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() => pipeline.EnviarAsync(new Mensagem("k", 2), ator));
        Assert.Equal(1, pipeline.ChamadasAoHandler);
    }

    [Fact]
    public async Task Reserva_em_andamento_bloqueia_ate_expirar_e_depois_pode_ser_assumida()
    {
        var pipeline = new Pipeline();
        var ator = new FakeCurrentUser();
        var mensagem = new Mensagem("k", 1);

        // Simula um processo que reservou e caiu antes do commit: a reserva ficou gravada.
        var request = new IdempotencyRequest(
            $"{ator.UserId}:{typeof(Mensagem).FullName}", "k",
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(mensagem))));
        pipeline.Banco.Registros[$"{request.Scope}|k"] = new IdempotencyRecord(false, request.PayloadHash, null, pipeline.Tempo.Agora);

        await Assert.ThrowsAsync<OperationInProgressException>(() => pipeline.EnviarAsync(mensagem, ator));

        pipeline.Tempo.Agora += new IdempotencyOptions().ReservationTimeout;
        var assumida = await pipeline.EnviarAsync(mensagem, ator);

        Assert.True(assumida.IsSuccess);
        Assert.Equal(1, pipeline.ChamadasAoHandler);
    }
}
