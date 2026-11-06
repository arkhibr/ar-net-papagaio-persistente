using SharedKernel;

namespace Chamados.Infrastructure;

/// <summary>Implementação de IDatabaseInitializer (SharedKernel) para o schema de Chamados.</summary>
internal sealed class ChamadosDatabaseInitializer : IDatabaseInitializer
{
    private readonly ChamadosDbContext _dbContext;

    public ChamadosDatabaseInitializer(ChamadosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task EnsureCreatedAsync(CancellationToken cancellationToken) =>
        _dbContext.Database.EnsureCreatedAsync(cancellationToken);
}
