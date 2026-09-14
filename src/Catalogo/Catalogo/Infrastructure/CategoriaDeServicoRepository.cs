using Catalogo.Application;
using Catalogo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure;

/// <summary>
/// Implementação real (EF Core) de ICategoriaDeServicoRepository. ListarTodasAsync é leitura
/// pura (no-tracking, arquitetura/27-leitura-e-query-side.md) — CategoriasDeServicoQuery nunca
/// altera o agregado. ObterPorIdAsync também é só leitura (usada por
/// ResolverEquipeESlaQueryHandler para resolver EquipeId/SLA, nunca para editar a categoria —
/// não há Command de escrita sobre CategoriaDeServico nesta rodada), então também no-tracking.
/// </summary>
internal sealed class CategoriaDeServicoRepository : ICategoriaDeServicoRepository
{
    private readonly CatalogoDbContext _dbContext;

    public CategoriaDeServicoRepository(CatalogoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CategoriaDeServico>> ListarTodasAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.CategoriasDeServico
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoriaDeServico?> ObterPorIdAsync(Guid categoriaId, CancellationToken cancellationToken)
    {
        return await _dbContext.CategoriasDeServico
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == categoriaId, cancellationToken);
    }
}
