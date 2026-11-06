namespace SharedKernel;

/// <summary>
/// A mesma Idempotency-Key chegou, do mesmo usuário e para o mesmo Command, com um payload
/// diferente do original: não é reenvio da mesma requisição. Traduzida para 422.
/// </summary>
public sealed class IdempotencyKeyReusedException : Exception
{
    public IdempotencyKeyReusedException(string message) : base(message)
    {
    }
}
