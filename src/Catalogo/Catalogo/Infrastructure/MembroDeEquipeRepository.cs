using Catalogo.Application;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure;

/// <summary>
/// Implementação real (EF Core) de IMembroDeEquipeRepository. Consultas puramente de leitura
/// (no-tracking) — não há Command de escrita sobre o vínculo técnico-equipe nesta rodada
/// (cadastro de membro fica fora de escopo, plano-de-arquitetura.md não lista nenhum Command
/// para isso).
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
        var membro = await _dbContext.MembrosDeEquipe
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.TecnicoId == tecnicoId, cancellationToken);

        return membro?.EquipeId;
    }
}
