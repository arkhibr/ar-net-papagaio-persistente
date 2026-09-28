using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Escalona o chamado. Só o ator de sistema (job de escalonamento) dispara; não tem rota HTTP.
/// Auditado pelo evento ChamadoEscalonado.
/// </summary>
public sealed record EscalonarChamadoCommand(Guid ChamadoId, string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, IRequiresAuthorization<IChamadosAuthorizationContext>, ITransactionalCommand
{
    public Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IChamadosAuthorizationContext context, CancellationToken cancellationToken) =>
        Task.FromResult(currentUser.IsSystemActor);
}
