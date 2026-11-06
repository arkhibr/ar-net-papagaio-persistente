using Mediator;
using SharedKernel.Modules;

namespace SharedKernel.Messaging;

/// <summary>
/// Envelope transacional mais interno, ao redor do Handler (arquitetura/25-transacao-e-unit-of-work.md):
/// Logging → Validation → Authorization → Idempotency → Caching → UnitOfWork → Handler. Roda
/// só para TMessage : ITransactionalCommand, com o IUnitOfWork do módulo dono da mensagem.
///
/// Um único SaveChanges por Command:
/// - sucesso: grava a mudança de negócio e, se a mensagem é idempotente, a conclusão da chave;
/// - Result.Failure de Command idempotente: descarta o que o handler encenou e grava só a
///   conclusão da chave (a falha determinística fica gravada para reenvios, arquitetura/06);
/// - Result.Failure de Command não idempotente: nenhum SaveChanges;
/// - exceção: propaga sem SaveChanges.
///
/// Fora do escopo (próximo passo): dispatch pós-commit de eventos de domínio in-process
/// (IDomainEventDispatcher, arquitetura/25). Os eventos já existem no agregado e são
/// consumidos hoje pela auditoria, dentro do IUnitOfWork do módulo.
/// </summary>
public sealed class UnitOfWorkBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage, ITransactionalCommand
    where TResponse : IResult
{
    private readonly IModuleService<IUnitOfWork> _unitsOfWork;
    private readonly PendingIdempotency _pendingIdempotency;

    public UnitOfWorkBehavior(IModuleService<IUnitOfWork> unitsOfWork, PendingIdempotency pendingIdempotency)
    {
        _unitsOfWork = unitsOfWork;
        _pendingIdempotency = pendingIdempotency;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var unitOfWork = _unitsOfWork.For(typeof(TMessage));
        var response = await next(message, cancellationToken);
        var idempotencia = _pendingIdempotency.For(message);

        if (idempotencia is null)
        {
            if (response.IsSuccess)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return response;
        }

        if (response.IsFailure)
        {
            unitOfWork.DiscardChanges();
        }

        await idempotencia.StageAsync(response, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        idempotencia.MarkCommitted();

        return response;
    }
}
