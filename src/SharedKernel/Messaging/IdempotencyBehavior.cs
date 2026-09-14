using System.Text.Json;
using Mediator;

namespace SharedKernel.Messaging;

/// <summary>
/// Pipeline behavior de idempotência (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md,
/// arquitetura/25-transacao-e-unit-of-work.md). Roda antes do UnitOfWork/Handler na ordem fixa
/// (Logging → Validation → Authorization → Idempotency → Caching → Handler).
///
/// Regra crítica (13-estrategia-de-testes.md): só falha de negócio determinística
/// (Result&lt;T&gt;.Failure devolvido pelo handler) é elegível a virar registro concluído — o
/// próprio "next" já devolveu um Result normalmente, então o behavior o marca como concluído
/// e cacheia. Uma exceção transitória (ex.: ConcurrencyException vinda do IUnitOfWork) PROPAGA
/// sem ser capturada e sem que CompleteAsync seja chamado: se fosse capturada e virasse
/// Result.Failure aqui, o behavior devolveria essa falha para sempre sob aquela chave, mesmo
/// que uma nova tentativa pudesse ter sucesso.
/// </summary>
public sealed class IdempotencyBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage, IIdempotentCommand
{
    private readonly IIdempotencyStore _store;

    public IdempotencyBehavior(IIdempotencyStore store)
    {
        _store = store;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var existing = await _store.FindAsync(message.IdempotencyKey, cancellationToken);

        if (existing is { IsCompleted: true })
        {
            return JsonSerializer.Deserialize<TResponse>(existing.SerializedResponse!)!;
        }

        if (existing is { IsCompleted: false })
        {
            throw new OperationInProgressException(
                $"Já existe uma operação em andamento para a chave '{message.IdempotencyKey}'.");
        }

        await _store.ReserveAsync(message.IdempotencyKey, cancellationToken);

        TResponse response;
        try
        {
            response = await next(message, cancellationToken);
        }
        catch
        {
            // Falha transitória de infraestrutura (ex.: ConcurrencyException do IUnitOfWork):
            // nunca vira Result.Failure aqui, nunca é marcada como concluída. Libera a reserva
            // para que uma nova tentativa com a mesma chave possa ter sucesso depois, em vez de
            // ficar travada como "em andamento" (OperationInProgressException para sempre).
            await _store.ReleaseAsync(message.IdempotencyKey, cancellationToken);
            throw;
        }

        // Chegou aqui: o handler devolveu normalmente (sucesso OU Result.Failure de negócio,
        // ambos determinísticos) — seguro cachear como concluído.
        await _store.CompleteAsync(message.IdempotencyKey, JsonSerializer.Serialize(response), cancellationToken);

        return response;
    }
}
