namespace SharedKernel;

/// <summary>
/// "Leitura B" de autorização (arquitetura/12-autorizacao-por-recurso.md): depende do conteúdo
/// da mensagem/dado de domínio, resolvida por AuthorizationBehavior no pipeline, antes do
/// handler rodar. O agregado nunca sabe que isto existe. Implemente a versão genérica,
/// tipada pelo contexto de autorização do módulo.
/// </summary>
public interface IRequiresAuthorization
{
    /// <summary>
    /// Quando true, a negação vira ResourceNotFoundException (404) em vez de
    /// AuthorizationDeniedException (403), para não revelar se o recurso existe.
    /// </summary>
    bool HideExistenceWhenDenied => false;

    Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken);
}

public interface IRequiresAuthorization<TContext> : IRequiresAuthorization
    where TContext : IAuthorizationContext
{
    Task<bool> IsAuthorizedAsync(ICurrentUser currentUser, TContext context, CancellationToken cancellationToken);

    Task<bool> IRequiresAuthorization.IsAuthorizedAsync(
        ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken) =>
        context is TContext typed
            ? IsAuthorizedAsync(currentUser, typed, cancellationToken)
            : throw new InvalidOperationException(
                $"'{GetType().Name}' espera {typeof(TContext).Name}, mas o módulo registrou {context.GetType().Name}.");
}
