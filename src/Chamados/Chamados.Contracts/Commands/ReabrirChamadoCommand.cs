using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>Reabre o chamado fechado. Autorizado só para o solicitante do chamado.</summary>
public sealed record ReabrirChamadoCommand(Guid ChamadoId, string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, IRequiresAuthorization<IChamadosAuthorizationContext>, ITransactionalCommand
{
    public Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IChamadosAuthorizationContext context, CancellationToken cancellationToken) =>
        context.EhSolicitanteAsync(currentUser.UserId, ChamadoId, cancellationToken);
}
