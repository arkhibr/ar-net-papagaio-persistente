namespace SharedKernel;

/// <summary>
/// Falha transitória de infraestrutura: conflito de gravação otimista (RowVersion),
/// traduzida a partir de DbUpdateConcurrencyException só por IUnitOfWork.SaveChangesAsync
/// (arquitetura/25-transacao-e-unit-of-work.md). Nunca vira Result.Failure: uma nova
/// tentativa pode ter sucesso, então não é seguro cachear pela idempotência.
/// </summary>
public sealed class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
