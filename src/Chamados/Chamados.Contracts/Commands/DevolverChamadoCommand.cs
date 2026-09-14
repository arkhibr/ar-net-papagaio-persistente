using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Técnico atribuído devolve o chamado à fila. Autorização: só o técnico atualmente
/// atribuído — comparação de conteúdo direta (TecnicoAtribuidoId chega no Command já
/// resolvido pelo handler? Não: a checagem de "quem está atribuído" depende do estado do
/// agregado, então usa IAuthorizationContext, que consulta o vínculo técnico-chamado
/// atual). Idempotency-Key exigida (especificacao-clarificada.md).
/// Tipo público em Contracts: tem rota HTTP (arquitetura/01).
/// </summary>
public sealed record DevolverChamadoCommand(Guid ChamadoId, string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, IRequiresAuthorization, ITransactionalCommand
{
    public Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken) =>
        context.HasResourceLinkAsync(currentUser.UserId, ChamadoId, cancellationToken);
}
