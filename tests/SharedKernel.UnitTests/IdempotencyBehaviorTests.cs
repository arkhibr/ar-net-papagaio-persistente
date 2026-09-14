using Mediator;
using SharedKernel;
using SharedKernel.Messaging;
using Xunit;

namespace SharedKernel.UnitTests;

/// <summary>
/// IdempotencyBehavior (13-estrategia-de-testes.md, arquitetura/06, arquitetura/25):
///
/// 1. Segunda chamada com a mesma Idempotency-Key, após sucesso, não deve executar o
///    handler de negócio de novo (devolve a resposta cacheada).
/// 2. Uma exceção transitória vinda do handler (representando o que o IUnitOfWork faria
///    ao capturar DbUpdateConcurrencyException e relançar como ConcurrencyException)
///    PROPAGA através do behavior sem que a operação seja marcada como concluída — porque
///    se fosse capturada e virasse Result.Failure, o behavior devolveria essa falha para
///    sempre sob aquela chave, mesmo que uma nova tentativa pudesse ter sucesso.
/// </summary>
public class IdempotencyBehaviorTests
{
    private sealed record MensagemDeTeste(string IdempotencyKey) : IRequest<Result<Guid>>, IIdempotentCommand;

    /// <summary>Store fake em memória; simula reserva/conclusão exatamente como o contrato exige.</summary>
    private sealed class FakeIdempotencyStore : IIdempotencyStore
    {
        private readonly Dictionary<string, IdempotencyRecord> _registros = new();

        public Task<IdempotencyRecord?> FindAsync(string idempotencyKey, CancellationToken cancellationToken) =>
            Task.FromResult(_registros.GetValueOrDefault(idempotencyKey));

        public Task ReserveAsync(string idempotencyKey, CancellationToken cancellationToken)
        {
            _registros[idempotencyKey] = new IdempotencyRecord(idempotencyKey, IsCompleted: false, SerializedResponse: null);
            return Task.CompletedTask;
        }

        public Task CompleteAsync(string idempotencyKey, string serializedResponse, CancellationToken cancellationToken)
        {
            _registros[idempotencyKey] = new IdempotencyRecord(idempotencyKey, IsCompleted: true, serializedResponse);
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(string idempotencyKey, CancellationToken cancellationToken)
        {
            _registros.Remove(idempotencyKey);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Segunda_chamada_com_a_mesma_chave_apos_sucesso_nao_deve_executar_o_handler_de_novo()
    {
        var store = new FakeIdempotencyStore();
        var behavior = new IdempotencyBehavior<MensagemDeTeste, Result<Guid>>(store);
        var mensagem = new MensagemDeTeste("chave-1");
        var chamadasAoHandler = 0;
        var valorGerado = Guid.NewGuid();

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next = (_, _) =>
        {
            chamadasAoHandler++;
            return ValueTask.FromResult(Result<Guid>.Success(valorGerado));
        };

        var primeiraResposta = await behavior.Handle(mensagem, next, CancellationToken.None);
        var segundaResposta = await behavior.Handle(mensagem, next, CancellationToken.None);

        Assert.Equal(1, chamadasAoHandler);
        Assert.True(primeiraResposta.IsSuccess);
        Assert.True(segundaResposta.IsSuccess);
        Assert.Equal(valorGerado, primeiraResposta.Value);
        Assert.Equal(valorGerado, segundaResposta.Value);
    }

    [Fact]
    public async Task Segunda_chamada_com_a_mesma_chave_apos_falha_de_negocio_determinística_nao_deve_executar_o_handler_de_novo()
    {
        var store = new FakeIdempotencyStore();
        var behavior = new IdempotencyBehavior<MensagemDeTeste, Result<Guid>>(store);
        var mensagem = new MensagemDeTeste("chave-2");
        var chamadasAoHandler = 0;

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next = (_, _) =>
        {
            chamadasAoHandler++;
            return ValueTask.FromResult(Result<Guid>.Failure("Regra de negócio violada."));
        };

        var primeiraResposta = await behavior.Handle(mensagem, next, CancellationToken.None);
        var segundaResposta = await behavior.Handle(mensagem, next, CancellationToken.None);

        Assert.Equal(1, chamadasAoHandler);
        Assert.True(primeiraResposta.IsFailure);
        Assert.True(segundaResposta.IsFailure);
        Assert.Equal("Regra de negócio violada.", segundaResposta.Error);
    }

    [Fact]
    public async Task Excecao_transitoria_do_handler_deve_propagar_sem_marcar_a_operacao_como_concluida()
    {
        var store = new FakeIdempotencyStore();
        var behavior = new IdempotencyBehavior<MensagemDeTeste, Result<Guid>>(store);
        var mensagem = new MensagemDeTeste("chave-3");
        var chamadasAoHandler = 0;

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next = (_, _) =>
        {
            chamadasAoHandler++;
            // Representa o que aconteceria se IUnitOfWork.SaveChangesAsync capturasse
            // DbUpdateConcurrencyException e relançasse como ConcurrencyException: falha
            // transitória de infraestrutura, nunca convertida para Result.Failure.
            throw new ConcurrencyException("O recurso foi alterado por outra operação.");
        };

        await Assert.ThrowsAsync<ConcurrencyException>(() => behavior.Handle(mensagem, next, CancellationToken.None).AsTask());

        Assert.Equal(1, chamadasAoHandler);

        // A reserva foi liberada (não fica travada como "em andamento" para sempre) — uma nova
        // tentativa com a mesma chave deve poder chamar o handler de novo, não deve ser
        // bloqueada nem devolver uma falha cacheada permanentemente.
        var registro = await store.FindAsync("chave-3", CancellationToken.None);
        Assert.Null(registro);

        // Nova tentativa deve poder chamar o handler de novo (por exemplo, com sucesso desta vez).
        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> nextComSucesso = (_, _) =>
        {
            chamadasAoHandler++;
            return ValueTask.FromResult(Result<Guid>.Success(Guid.NewGuid()));
        };

        var novaResposta = await behavior.Handle(mensagem, nextComSucesso, CancellationToken.None);

        Assert.Equal(2, chamadasAoHandler);
        Assert.True(novaResposta.IsSuccess);
    }

    [Fact]
    public async Task Segunda_chamada_concorrente_enquanto_a_primeira_ainda_esta_em_andamento_deve_lancar_OperationInProgressException()
    {
        var store = new FakeIdempotencyStore();
        await store.ReserveAsync("chave-4", CancellationToken.None); // simula reserva feita por uma requisição concorrente ainda em voo

        var behavior = new IdempotencyBehavior<MensagemDeTeste, Result<Guid>>(store);
        var mensagem = new MensagemDeTeste("chave-4");

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next =
            (_, _) => ValueTask.FromResult(Result<Guid>.Success(Guid.NewGuid()));

        await Assert.ThrowsAsync<OperationInProgressException>(
            () => behavior.Handle(mensagem, next, CancellationToken.None).AsTask());
    }
}
