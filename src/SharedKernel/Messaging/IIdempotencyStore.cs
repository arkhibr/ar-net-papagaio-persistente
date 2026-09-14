namespace SharedKernel.Messaging;

/// <summary>
/// Porta estreita que o IdempotencyBehavior usa para reservar/consultar/concluir uma
/// Idempotency-Key. Implementação real mora na Infrastructure (mesmo DbContext scoped
/// do UnitOfWork, ver arquitetura/25-transacao-e-unit-of-work.md); aqui só o contrato,
/// para o behavior poder ser testado com um fake em Application.UnitTests.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Registro de uma chave já vista, se existir. Nulo quando é a primeira chegada.
    /// </summary>
    Task<IdempotencyRecord?> FindAsync(string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>
    /// Reserva a chave como "em andamento". Chamada só quando FindAsync não achou nada.
    /// </summary>
    Task ReserveAsync(string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>
    /// Marca a chave como concluída, guardando a resposta serializada para reenvios futuros.
    /// Encenada no mesmo DbContext do UnitOfWork; a marcação em si não é responsabilidade
    /// deste stub decidir quando committar — isso é o UnitOfWorkBehavior.
    /// </summary>
    Task CompleteAsync(string idempotencyKey, string serializedResponse, CancellationToken cancellationToken);

    /// <summary>
    /// Libera uma reserva "em andamento" sem concluí-la, chamada quando o handler propaga uma
    /// falha transitória de infraestrutura (ex.: ConcurrencyException) — nunca chamada para
    /// falha de negócio determinística (Result.Failure), que é sempre concluída via
    /// CompleteAsync. Sem isso, a chave ficaria travada como "em andamento" para sempre,
    /// bloqueando qualquer nova tentativa legítima com OperationInProgressException.
    /// </summary>
    Task ReleaseAsync(string idempotencyKey, CancellationToken cancellationToken);
}

public sealed record IdempotencyRecord(string IdempotencyKey, bool IsCompleted, string? SerializedResponse);
