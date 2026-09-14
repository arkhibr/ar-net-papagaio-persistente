using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Resolve o chamado (nota de resolução obrigatória). Autorização: só o técnico atualmente
/// atribuído, consultando o vínculo atual via IAuthorizationContext (o "atribuído" é estado
/// do agregado no momento, não algo o Command já sabe de antemão). Idempotency-Key exigida.
/// Tipo público em Contracts: tem rota HTTP (arquitetura/01).
///
/// IAuditable: resolução está no subconjunto auditado (plano-de-arquitetura.md secao 7;
/// especificacao-clarificada.md).
/// </summary>
public sealed record ResolverChamadoCommand(Guid ChamadoId, string NotaResolucao, string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, IRequiresAuthorization, IAuditable, ITransactionalCommand
{
    public Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken) =>
        context.HasResourceLinkAsync(currentUser.UserId, ChamadoId, cancellationToken);
}
