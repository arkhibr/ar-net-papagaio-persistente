using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>O técnico atribuído resolve o chamado. Auditado pelo evento ChamadoResolvido.</summary>
public sealed record ResolverChamadoCommand(Guid ChamadoId, string NotaResolucao, string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, IRequiresAuthorization<IChamadosAuthorizationContext>, ITransactionalCommand
{
    public Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IChamadosAuthorizationContext context, CancellationToken cancellationToken) =>
        context.EhTecnicoAtribuidoAsync(currentUser.UserId, ChamadoId, cancellationToken);
}
