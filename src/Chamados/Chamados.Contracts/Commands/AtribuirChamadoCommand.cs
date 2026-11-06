using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Autoatribuição: o técnico autenticado pega um chamado da fila da própria equipe.
/// TecnicoId precisa ser o próprio usuário autenticado (a Api monta a partir de ICurrentUser, e
/// IsAuthorizedAsync recusa qualquer outro valor, para qualquer chamador de Contracts). RowVersion
/// vem do If-Match (arquitetura/06). Auditado pelo evento ChamadoAtribuido.
/// </summary>
public sealed record AtribuirChamadoCommand(
    Guid ChamadoId,
    Guid TecnicoId,
    byte[] RowVersion,
    string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, IRequiresAuthorization<IChamadosAuthorizationContext>, ITransactionalCommand
{
    public async Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IChamadosAuthorizationContext context, CancellationToken cancellationToken) =>
        currentUser.IsInRole("Tecnico")
        && TecnicoId == currentUser.UserId
        && await context.EhMembroDaEquipeResponsavelAsync(currentUser.UserId, ChamadoId, cancellationToken);
}
