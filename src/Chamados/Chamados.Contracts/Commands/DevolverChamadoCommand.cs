using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>O técnico atribuído devolve o chamado à fila.</summary>
public sealed record DevolverChamadoCommand(Guid ChamadoId, string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, IRequiresAuthorization<IChamadosAuthorizationContext>, ITransactionalCommand
{
    public Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IChamadosAuthorizationContext context, CancellationToken cancellationToken) =>
        context.EhTecnicoAtribuidoAsync(currentUser.UserId, ChamadoId, cancellationToken);
}
