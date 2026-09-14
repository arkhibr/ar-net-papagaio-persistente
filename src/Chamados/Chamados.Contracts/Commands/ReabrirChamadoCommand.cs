using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Reabre um chamado fechado (dentro de 5 dias corridos do fechamento). Autorização: só o
/// solicitante — mesmo padrão de "recurso próprio" de AlterarProprioPerfilCommand em
/// 12-autorizacao-por-recurso.md, comparação direta de conteúdo, sem tocar IAuthorizationContext.
/// Tipo público em Contracts: tem rota HTTP (arquitetura/01).
/// </summary>
public sealed record ReabrirChamadoCommand(Guid ChamadoId, Guid SolicitanteId, string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, IRequiresAuthorization, ITransactionalCommand
{
    public Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken) =>
        Task.FromResult(currentUser.UserId == SolicitanteId);
}
