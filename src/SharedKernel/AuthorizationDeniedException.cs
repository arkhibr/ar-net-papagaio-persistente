namespace SharedKernel;

/// <summary>
/// Lançada pelo AuthorizationBehavior quando IRequiresAuthorization.IsAuthorizedAsync
/// devolve false. Traduzida para HTTP 403 pelo GlobalExceptionHandler
/// (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md).
/// </summary>
public sealed class AuthorizationDeniedException : Exception
{
    public AuthorizationDeniedException(string message) : base(message)
    {
    }
}
