using SharedKernel;

namespace Catalogo.Infrastructure;

/// <summary>Implementação de IDatabaseInitializer (SharedKernel) para o schema de Catalogo.</summary>
internal sealed class CatalogoDatabaseInitializer : IDatabaseInitializer
{
    private readonly CatalogoDbContext _dbContext;

    public CatalogoDatabaseInitializer(CatalogoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task EnsureCreatedAsync(CancellationToken cancellationToken) =>
        _dbContext.Database.EnsureCreatedAsync(cancellationToken);
}
