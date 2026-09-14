namespace SharedKernel;

/// <summary>
/// Serviço estreito de consulta de vínculo ator-recurso, implementado na Infrastructure
/// (arquitetura/12-autorizacao-por-recurso.md). Não é ICurrentUser: mantém a identidade
/// livre de acesso a banco/conceito de domínio como "recurso".
/// </summary>
public interface IAuthorizationContext
{
    Task<bool> HasResourceLinkAsync(Guid userId, Guid resourceId, CancellationToken cancellationToken);
}
