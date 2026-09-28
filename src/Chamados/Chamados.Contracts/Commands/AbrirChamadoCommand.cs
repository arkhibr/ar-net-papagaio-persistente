using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Abre um chamado. SolicitanteId vem sempre de ICurrentUser na Api, nunca do corpo, e
/// IsAuthorizedAsync recusa qualquer outro valor, para qualquer chamador de Contracts (mesma
/// regra de AtribuirChamadoCommand, A1 de achados.md): ninguém abre chamado em nome de outra
/// pessoa. O ator de sistema também não abre chamado. A abertura é auditada pelo evento de
/// domínio ChamadoAberto (arquitetura/21).
/// </summary>
public sealed record AbrirChamadoCommand(
    Guid SolicitanteId,
    Guid CategoriaId,
    PrioridadeChamado Prioridade,
    string IdempotencyKey)
    : IRequest<Result<Guid>>, IIdempotentCommand, IRequiresAuthorization<IChamadosAuthorizationContext>, ITransactionalCommand
{
    public Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IChamadosAuthorizationContext context, CancellationToken cancellationToken) =>
        Task.FromResult(
            currentUser.IsAuthenticated
            && !currentUser.IsSystemActor
            && SolicitanteId != Guid.Empty
            && SolicitanteId == currentUser.UserId);
}
