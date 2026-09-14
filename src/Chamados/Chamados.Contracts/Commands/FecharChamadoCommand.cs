using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Fecha o chamado resolvido. Autorização: SolicitanteId chega no próprio Command (dado do
/// domínio, congelado na abertura) OU ator de sistema (job de fechamento automático após 3
/// dias, plano-de-arquitetura.md secao 3/5). currentUser.UserId == SolicitanteId cobre o
/// solicitante; currentUser.IsSystemActor cobre o job — nenhum dos dois toca
/// IAuthorizationContext, então a checagem não precisa consultar infraestrutura.
/// Tipo público em Contracts: tem rota HTTP (arquitetura/01).
/// </summary>
public sealed record FecharChamadoCommand(Guid ChamadoId, Guid SolicitanteId, string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, IRequiresAuthorization, ITransactionalCommand
{
    public Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken) =>
        Task.FromResult(currentUser.IsSystemActor || currentUser.UserId == SolicitanteId);
}
