namespace SharedKernel.Messaging;

/// <summary>
/// Identidade de uma requisição idempotente: a chave do cliente vale só dentro do Scope
/// (usuário + tipo do Command); PayloadHash distingue um reenvio legítimo de um reuso da chave
/// para outra requisição (arquitetura/06, "protege contra reenvio da mesma requisição").
/// </summary>
public sealed record IdempotencyRequest(string Scope, string Key, string PayloadHash);

public sealed record IdempotencyRecord(
    bool IsCompleted, string PayloadHash, string? SerializedResponse, DateTimeOffset ReservedAt);

/// <summary>
/// Porta que o IdempotencyBehavior/UnitOfWorkBehavior usam para a Idempotency-Key. A
/// implementação real mora na Infrastructure de cada módulo, no mesmo DbContext do IUnitOfWork
/// daquele módulo (arquitetura/25, "Idempotência participa da mesma transação").
///
/// Transações: a reserva é o único passo gravado sozinho, antes do handler, porque precisa
/// ficar visível para uma requisição concorrente (lacuna D1 de achados.md). A conclusão é só
/// encenada (StageCompletionAsync) e vai no mesmo SaveChanges do UnitOfWorkBehavior que grava a
/// mudança de negócio.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(IdempotencyRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Grava a reserva "em andamento" (commit próprio). Se outra requisição reservou a mesma
    /// chave antes (corrida), lança OperationInProgressException.
    /// </summary>
    Task ReserveAsync(IdempotencyRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Assume uma reserva expirada (processo caiu antes do commit). Só uma requisição vence:
    /// devolve false se outra já assumiu ou concluiu a reserva desde a leitura.
    /// </summary>
    Task<bool> TryRenewReservationAsync(
        IdempotencyRequest request, DateTimeOffset previousReservedAt, CancellationToken cancellationToken);

    /// <summary>Encena a conclusão no DbContext do módulo, sem gravar; o commit é do UnitOfWorkBehavior.</summary>
    Task StageCompletionAsync(IdempotencyRequest request, string serializedResponse, CancellationToken cancellationToken);

    /// <summary>
    /// Conclui com commit próprio. Só para Command idempotente que não é ITransactionalCommand
    /// (não passa pelo UnitOfWorkBehavior).
    /// </summary>
    Task CompleteAsync(IdempotencyRequest request, string serializedResponse, CancellationToken cancellationToken);

    /// <summary>
    /// Descarta o que estiver pendente no DbContext e apaga a reserva, com commit próprio.
    /// Chamada quando o handler ou o commit lançam exceção; nunca grava a mudança de negócio.
    /// </summary>
    Task ReleaseAsync(IdempotencyRequest request, CancellationToken cancellationToken);
}
