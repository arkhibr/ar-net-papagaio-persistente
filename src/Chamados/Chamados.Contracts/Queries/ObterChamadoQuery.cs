using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Detalhe de um chamado. Visível para o solicitante, o técnico atribuído e técnicos/supervisores
/// da equipe responsável. A autorização roda no pipeline, antes do handler carregar qualquer
/// coisa, e a negação responde como "não encontrado" (HideExistenceWhenDenied), para que "não
/// existe" e "sem acesso" sejam indistinguíveis (M3 de achados.md).
/// </summary>
public sealed record ObterChamadoQuery(Guid ChamadoId)
    : IRequest<Result<ChamadoDetalheVersionado>>, IRequiresAuthorization<IChamadosAuthorizationContext>
{
    public bool HideExistenceWhenDenied => true;

    public async Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IChamadosAuthorizationContext context, CancellationToken cancellationToken) =>
        await context.EhSolicitanteAsync(currentUser.UserId, ChamadoId, cancellationToken)
        || await context.EhTecnicoAtribuidoAsync(currentUser.UserId, ChamadoId, cancellationToken)
        || ((currentUser.IsInRole("Tecnico") || currentUser.IsInRole("Supervisor"))
            && await context.EhMembroDaEquipeResponsavelAsync(currentUser.UserId, ChamadoId, cancellationToken));
}
