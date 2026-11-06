using Catalogo.Application;
using Catalogo.Contracts;
using Catalogo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure;

internal sealed class EquipeRepository : IEquipeRepository
{
    private readonly CatalogoDbContext _dbContext;

    public EquipeRepository(CatalogoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<EquipeDto>> ListarComMembrosAsync(CancellationToken cancellationToken)
    {
        var equipes = await _dbContext.Equipes.AsNoTracking()
            .OrderBy(e => e.Nome)
            .Select(e => new { e.Id, e.Nome })
            .ToListAsync(cancellationToken);
        var membros = await _dbContext.MembrosDeEquipe.AsNoTracking()
            .Select(m => new { m.EquipeId, m.TecnicoId })
            .ToListAsync(cancellationToken);
        var porEquipe = membros.ToLookup(m => m.EquipeId, m => m.TecnicoId);

        return equipes.Select(e => new EquipeDto(e.Id, e.Nome, porEquipe[e.Id].ToList())).ToList();
    }

    public Task<bool> ExisteAsync(Guid equipeId, CancellationToken cancellationToken) =>
        _dbContext.Equipes.AnyAsync(e => e.Id == equipeId, cancellationToken);

    public async Task AdicionarAsync(Equipe equipe, CancellationToken cancellationToken)
    {
        await _dbContext.Equipes.AddAsync(equipe, cancellationToken);
    }

    public Task<Equipe?> ObterParaEscritaAsync(Guid equipeId, CancellationToken cancellationToken) =>
        _dbContext.Equipes.SingleOrDefaultAsync(e => e.Id == equipeId, cancellationToken);
}
