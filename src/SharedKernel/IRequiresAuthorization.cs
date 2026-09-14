namespace SharedKernel;

/// <summary>
/// Interface marcadora da "leitura B" de autorização (arquitetura/12-autorizacao-por-recurso.md):
/// depende do conteúdo do Command/dado de domínio, resolvida por AuthorizationBehavior no
/// pipeline, antes do handler rodar. O agregado nunca sabe que isto existe.
/// </summary>
public interface IRequiresAuthorization
{
    Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken);
}
