namespace SharedKernel;

/// <summary>
/// Lançada pelo IdempotencyBehavior quando uma segunda requisição concorrente chega com a
/// mesma Idempotency-Key enquanto a primeira ainda está em andamento (reserva não concluída).
/// Traduzida para HTTP 409 (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md).
/// </summary>
public sealed class OperationInProgressException : Exception
{
    public OperationInProgressException(string message) : base(message)
    {
    }
}
