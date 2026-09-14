using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Marca o chamado como escalonado. Só ator de sistema (job de SLA no Worker, nunca exposto
/// via API pública, plano-de-arquitetura.md secao 3/5). IdempotencyKey é gerada pelo próprio
/// job (ChamadoId + Ação + ciclo), não por header HTTP — mas o mecanismo do Command
/// (IIdempotentCommand) é o mesmo, agnóstico a HTTP.
/// Tipo público em Contracts: sem rota HTTP, mas despachado pelo Worker, que só referencia
/// Contracts, nunca o interior do módulo (arquitetura/01).
///
/// IAuditable: escalonamento está no subconjunto auditado (plano-de-arquitetura.md secao 7;
/// especificacao-clarificada.md). Ator do escalonamento é o identificador técnico do job, nunca
/// um usuário fabricado.
/// </summary>
public sealed record EscalonarChamadoCommand(Guid ChamadoId, string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, IRequiresAuthorization, IAuditable, ITransactionalCommand
{
    public Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken) =>
        Task.FromResult(currentUser.IsSystemActor);
}
