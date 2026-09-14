using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Autoatribuição: técnico da equipe responsável pega o chamado da fila para si.
/// Autorização (leitura B, IRequiresAuthorization): papel Técnico + ser membro da equipe
/// responsável pelo chamado — o vínculo técnico-equipe é dado do módulo Catalogo, por isso
/// a checagem consulta IAuthorizationContext (que atravessa Catalogo.Contracts na
/// implementação real de Infrastructure; plano-de-arquitetura.md secao 5, nota).
/// Idempotency-Key + RowVersion (concorrência: dois técnicos disputando a mesma fila).
/// Tipo público em Contracts: tem rota HTTP (arquitetura/01).
///
/// IAuditable: atribuição está no subconjunto auditado (plano-de-arquitetura.md secao 7;
/// especificacao-clarificada.md).
/// </summary>
public sealed record AtribuirChamadoCommand(
    Guid ChamadoId,
    Guid TecnicoId,
    byte[] RowVersion,
    string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, IRequiresAuthorization, IAuditable, ITransactionalCommand
{
    public async Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken)
    {
        if (!currentUser.IsInRole("Tecnico"))
        {
            return false;
        }

        return await context.HasResourceLinkAsync(currentUser.UserId, ChamadoId, cancellationToken);
    }
}
