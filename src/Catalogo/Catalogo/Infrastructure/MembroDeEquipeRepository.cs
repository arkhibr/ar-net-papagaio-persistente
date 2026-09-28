using Catalogo.Application;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure;

/// <summary>
/// Implementação real (EF Core) de IMembroDeEquipeRepository. Consultas no-tracking; vincular e
/// desvincular só encenam a mudança, e o commit é do UnitOfWorkBehavior (arquitetura/25).
/// </summary>
internal sealed class MembroDeEquipeRepository : IMembroDeEquipeRepository
{
    private readonly CatalogoDbContext _dbContext;

    public MembroDeEquipeRepository(CatalogoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> EhMembroAsync(Guid tecnicoId, Guid equipeId, CancellationToken cancellationToken)
    {
        return await _dbContext.MembrosDeEquipe
            .AsNoTracking()
            .AnyAsync(m => m.TecnicoId == tecnicoId && m.EquipeId == equipeId, cancellationToken);
    }

    public async Task<Guid?> ResolverEquipeIdAsync(Guid tecnicoId, CancellationToken cancellationToken)
    {
        return await _dbContext.MembrosDeEquipe
            .AsNoTracking()
            .Where(m => m.TecnicoId == tecnicoId)
            .Select(m => (Guid?)m.EquipeId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task VincularAsync(Guid usuarioId, Guid equipeId, CancellationToken cancellationToken)
    {
        await _dbContext.MembrosDeEquipe.AddAsync(new MembroDeEquipe(usuarioId, equipeId), cancellationToken);
    }

    public async Task<bool> DesvincularAsync(Guid usuarioId, Guid equipeId, CancellationToken cancellationToken)
    {
        var membro = await _dbContext.MembrosDeEquipe
            .SingleOrDefaultAsync(m => m.TecnicoId == usuarioId && m.EquipeId == equipeId, cancellationToken);
        if (membro is null)
        {
            return false;
        }

        _dbContext.MembrosDeEquipe.Remove(membro);
        return true;
    }
}
