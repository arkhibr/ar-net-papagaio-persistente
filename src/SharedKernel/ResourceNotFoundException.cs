namespace SharedKernel;

/// <summary>
/// Lançada pelo AuthorizationBehavior no lugar de AuthorizationDeniedException quando a
/// mensagem pede para não revelar a existência do recurso (IRequiresAuthorization
/// .HideExistenceWhenDenied). Traduzida para o mesmo 404 de "não encontrado", de modo que
/// "não existe" e "sem acesso" são indistinguíveis para o cliente.
/// </summary>
public sealed class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string message) : base(message)
    {
    }
}
