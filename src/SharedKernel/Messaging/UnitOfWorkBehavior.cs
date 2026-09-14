using Mediator;

namespace SharedKernel.Messaging;

/// <summary>
/// Envelope transacional mais interno, ao redor do Handler (arquitetura/25-transacao-e-unit-of-work.md):
/// Logging → Validation → Authorization → Idempotency → Caching → UnitOfWork → Handler. Roda
/// só para TMessage : ITransactionalCommand (marcador novo desta rodada — ver ITransactionalCommand
/// para o porquê de não reaproveitar IIdempotentCommand). Query nunca implementa
/// ITransactionalCommand, então nunca passa por este behavior: leitura não tem transação de
/// escrita nem commit.
///
/// Chama next() primeiro; só se a resposta for sucesso (TResponse : IResult, a mesma
/// interface-base de Result/Result&lt;T&gt;) chama IUnitOfWork.SaveChangesAsync. Result.Failure
/// ou qualquer exceção não capturada fazem rollback implícito (SaveChanges nunca é chamado) —
/// não há transação explícita de banco aberta por este behavior (isso é decisão de
/// Infrastructure/DbContext, arquitetura/25 aceita o commit implícito do SaveChanges como
/// fronteira transacional padrão).
///
/// Fora do escopo desta implementação (não implementado aqui, registrado como próximo passo):
/// o dispatch pós-commit de eventos de domínio in-process via IDomainEventDispatcher, citado no
/// exemplo de referência de arquitetura/25. Não existe ainda IDomainEventDispatcher nem
/// implementação de Infrastructure para ele nesta solution — introduzi-lo é trabalho de uma
/// rodada futura (quando um INotificationHandler reativo entre módulos precisar dele, ver
/// arquitetura/04-comunicacao-entre-modulos.md/arquitetura/05-processamento-assincrono-e-eventos.md),
/// não inventado agora sem consumidor real.
/// </summary>
public sealed class UnitOfWorkBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage, ITransactionalCommand
    where TResponse : IResult
{
    private readonly IUnitOfWork _unitOfWork;

    public UnitOfWorkBehavior(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(message, cancellationToken);

        if (response.IsFailure)
        {
            // Rollback implícito: SaveChanges nunca é chamado para uma falha de negócio
            // determinística (Result.Failure).
            return response;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }
}
