using System.Security.Cryptography;
using System.Text.Json;
using Mediator;
using Microsoft.Extensions.Options;
using SharedKernel.Modules;

namespace SharedKernel.Messaging;

/// <summary>
/// Pipeline behavior de idempotência (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md,
/// arquitetura/25-transacao-e-unit-of-work.md). Ordem fixa: Logging → Validation → Authorization →
/// Idempotency → Caching → UnitOfWork → Handler.
///
/// Escopo: a chave vale por (usuário, tipo do Command). O hash do payload separa reenvio
/// (mesmo corpo → devolve a resposta gravada) de reuso da chave (corpo diferente → 422).
///
/// Transações: só a reserva é gravada antes do handler. A conclusão (sucesso ou
/// Result.Failure determinístico) é encenada pelo UnitOfWorkBehavior no mesmo SaveChanges da
/// mudança de negócio. Exceção (transitória ou não) libera a reserva sem gravar nada do
/// handler e propaga: nunca vira Result.Failure cacheado.
/// </summary>
public sealed class IdempotencyBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage, IIdempotentCommand
{
    private readonly IModuleService<IIdempotencyStore> _stores;
    private readonly ICurrentUser _currentUser;
    private readonly PendingIdempotency _pending;
    private readonly TimeProvider _timeProvider;
    private readonly IdempotencyOptions _options;

    public IdempotencyBehavior(
        IModuleService<IIdempotencyStore> stores,
        ICurrentUser currentUser,
        PendingIdempotency pending,
        TimeProvider timeProvider,
        IOptions<IdempotencyOptions> options)
    {
        _stores = stores;
        _currentUser = currentUser;
        _pending = pending;
        _timeProvider = timeProvider;
        _options = options.Value;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var store = _stores.For(typeof(TMessage));
        var request = new IdempotencyRequest(
            Scope: $"{_currentUser.UserId}:{typeof(TMessage).FullName}",
            Key: message.IdempotencyKey,
            PayloadHash: HashDoPayload(message));

        var existing = await store.FindAsync(request, cancellationToken);

        if (existing is not null)
        {
            if (existing.PayloadHash != request.PayloadHash)
            {
                throw new IdempotencyKeyReusedException(
                    "A Idempotency-Key informada já foi usada para uma requisição com conteúdo diferente.");
            }

            if (existing.IsCompleted)
            {
                return JsonSerializer.Deserialize<TResponse>(existing.SerializedResponse!)!;
            }

            var expirada = _timeProvider.GetUtcNow() - existing.ReservedAt >= _options.ReservationTimeout;
            if (!expirada || !await store.TryRenewReservationAsync(request, existing.ReservedAt, cancellationToken))
            {
                throw new OperationInProgressException(
                    "Já existe uma operação em andamento para esta Idempotency-Key.");
            }
        }
        else
        {
            await store.ReserveAsync(request, cancellationToken);
        }

        var entry = _pending.Begin(message, (response, ct) =>
            store.StageCompletionAsync(request, JsonSerializer.Serialize((TResponse)response!), ct));

        try
        {
            TResponse response;
            try
            {
                response = await next(message, cancellationToken);
            }
            catch
            {
                // Nada do handler é gravado, e a reserva é liberada para uma nova tentativa.
                // CancellationToken.None: um cliente que cancelou não pode deixar a chave travada.
                await store.ReleaseAsync(request, CancellationToken.None);
                throw;
            }

            if (!entry.Committed)
            {
                // Command idempotente sem ITransactionalCommand: não passou pelo UnitOfWorkBehavior.
                await store.CompleteAsync(request, JsonSerializer.Serialize(response), cancellationToken);
            }

            return response;
        }
        finally
        {
            _pending.End(message);
        }
    }

    private static string HashDoPayload(TMessage message) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(message)));
}
