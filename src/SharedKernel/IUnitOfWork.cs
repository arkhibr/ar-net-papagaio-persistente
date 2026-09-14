namespace SharedKernel;

/// <summary>
/// Único ponto da solução que chama SaveChanges e nomeia DbUpdateConcurrencyException
/// (arquitetura/25-transacao-e-unit-of-work.md). Aqui só a assinatura mínima usada pelos
/// testes de Application via fake; a implementação real (EF Core) mora na Infrastructure.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
