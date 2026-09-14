namespace Chamados.Application;

/// <summary>
/// Porta usada por FilaDaEquipeQueryHandler para resolver a EquipeId do técnico/supervisor
/// autenticado (filtro por linha, arquitetura/14-filtro-de-dados.md) — nunca aceita EquipeId
/// vindo do cliente. Implementação real na Infrastructure consulta o vínculo
/// usuário-equipe, dado do módulo Catalogo.
/// </summary>
internal interface IEquipeDoUsuarioResolver
{
    Task<Guid?> ResolverEquipeIdAsync(Guid userId, CancellationToken cancellationToken);
}
