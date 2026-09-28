using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Reclassifica a prioridade. Autorizado para o técnico atribuído, ou para um técnico da equipe
/// responsável enquanto o chamado está na fila (Aberto). A regra roda no AuthorizationBehavior,
/// antes da idempotência: uma tentativa negada nunca reserva a chave (M4 de achados.md).
/// </summary>
public sealed record ReclassificarChamadoCommand(
    Guid ChamadoId,
    PrioridadeChamado NovaPrioridade,
    string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, IRequiresAuthorization<IChamadosAuthorizationContext>, ITransactionalCommand
{
    public async Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IChamadosAuthorizationContext context, CancellationToken cancellationToken) =>
        await context.EhTecnicoAtribuidoAsync(currentUser.UserId, ChamadoId, cancellationToken)
        || await context.EstaNaFilaDaEquipeDoUsuarioAsync(currentUser.UserId, ChamadoId, cancellationToken);
}
