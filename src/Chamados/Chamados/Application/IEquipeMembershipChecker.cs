namespace Chamados.Application;

/// <summary>
/// Porta usada pelo guard explícito de ReclassificarChamadoCommandHandler (não é
/// IRequiresAuthorization padrão, ver ReclassificarChamadoCommand) para checar se um
/// técnico é membro de uma equipe. Implementação real na Infrastructure consulta o vínculo
/// técnico-equipe, dado do módulo Catalogo (plano-de-arquitetura.md secao 5, nota).
/// </summary>
internal interface IEquipeMembershipChecker
{
    Task<bool> EhMembroDaEquipeAsync(Guid tecnicoId, Guid equipeId, CancellationToken cancellationToken);
}
