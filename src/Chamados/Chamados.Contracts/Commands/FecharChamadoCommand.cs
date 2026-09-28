using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Fecha o chamado resolvido. Autorizado para o solicitante do chamado (conferido no estado
/// persistido, nunca num campo informado por quem chama) ou para o ator de sistema (job de
/// fechamento automático).
/// </summary>
public sealed record FecharChamadoCommand(Guid ChamadoId, string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, IRequiresAuthorization<IChamadosAuthorizationContext>, ITransactionalCommand
{
    public async Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IChamadosAuthorizationContext context, CancellationToken cancellationToken) =>
        currentUser.IsSystemActor
        || await context.EhSolicitanteAsync(currentUser.UserId, ChamadoId, cancellationToken);
}
