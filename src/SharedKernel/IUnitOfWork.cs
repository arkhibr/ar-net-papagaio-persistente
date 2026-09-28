namespace SharedKernel;

/// <summary>
/// Único ponto da solução que chama SaveChanges e nomeia DbUpdateConcurrencyException
/// (arquitetura/25-transacao-e-unit-of-work.md). Um por módulo, registrado keyed pela chave do
/// módulo (ver SharedKernel.Modules).
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Descarta tudo o que o handler encenou e ainda não foi gravado. Usado quando o handler
    /// devolve Result.Failure e só o registro de idempotência deve ir para o commit.
    /// </summary>
    void DiscardChanges();
}
