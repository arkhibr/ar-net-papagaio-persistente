namespace Chamados.Infrastructure;

/// <summary>
/// Tabela técnica do IIdempotencyStore deste módulo (arquitetura/22: tabela técnica segue a
/// fronteira do módulo). Unicidade em (Scope, IdempotencyKey): a chave do cliente vale só para
/// o mesmo usuário e o mesmo tipo de Command. PayloadHash separa reenvio de reuso da chave.
/// </summary>
internal sealed class IdempotencyRecordEntity
{
    public Guid Id { get; private set; }
    public string Scope { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public bool IsCompleted { get; private set; }
    public string? SerializedResponse { get; private set; }
    public DateTimeOffset ReservedAt { get; private set; }

    private IdempotencyRecordEntity()
    {
    }

    public static IdempotencyRecordEntity Reserve(string scope, string idempotencyKey, string payloadHash, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Scope = scope,
        IdempotencyKey = idempotencyKey,
        PayloadHash = payloadHash,
        IsCompleted = false,
        SerializedResponse = null,
        ReservedAt = now,
    };

    public void Complete(string serializedResponse)
    {
        IsCompleted = true;
        SerializedResponse = serializedResponse;
    }
}
