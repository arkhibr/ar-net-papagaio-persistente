namespace SharedKernel;

/// <summary>
/// O If-Match enviado pelo cliente não corresponde mais ao RowVersion atual do recurso,
/// detectado antes de aplicar a mudança. Traduzida para 412
/// (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md, "ETag/If-Match").
/// </summary>
public sealed class PreconditionFailedException : Exception
{
    public PreconditionFailedException(string message) : base(message)
    {
    }
}
