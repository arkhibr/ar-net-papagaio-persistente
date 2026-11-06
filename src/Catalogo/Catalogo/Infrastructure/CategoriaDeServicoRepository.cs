using Catalogo.Application;
using Catalogo.Contracts;
using Catalogo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalogo.Infrastructure;

internal sealed class CategoriaDeServicoRepository : ICategoriaDeServicoRepository
{
    private readonly CatalogoDbContext _dbContext;

    public CategoriaDeServicoRepository(CatalogoDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CategoriaDeServicoDto>> ListarAsync(bool incluirInativas, CancellationToken cancellationToken)
    {
        var categorias = await _dbContext.CategoriasDeServico
            .AsNoTracking()
            .Where(c => incluirInativas || c.Ativa)
            .OrderBy(c => c.Nome)
            .Select(c => new
            {
                c.Id,
                c.Nome,
                c.EquipeId,
                EquipeNome = _dbContext.Equipes.Where(e => e.Id == c.EquipeId).Select(e => e.Nome).FirstOrDefault(),
                c.Ativa,
                Slas = c.Slas.Select(s => new { s.Prioridade, s.Horas }).ToList(),
            })
            .ToListAsync(cancellationToken);

        return categorias
            .Select(c => new CategoriaDeServicoDto(
                c.Id, c.Nome, c.EquipeId, c.EquipeNome, c.Ativa,
                c.Slas.OrderBy(s => s.Prioridade).Select(s => new SlaDto(s.Prioridade, s.Horas)).ToList()))
            .ToList();
    }

    public async Task<(Guid EquipeId, int? HorasDeSla, bool Ativa)?> ObterEquipeESlaAsync(
        Guid categoriaId, PrioridadeServico prioridade, CancellationToken cancellationToken)
    {
        var linha = await _dbContext.CategoriasDeServico
            .AsNoTracking()
            .Where(c => c.Id == categoriaId)
            .Select(c => new
            {
                c.EquipeId,
                c.Ativa,
                HorasDeSla = c.Slas.Where(s => s.Prioridade == prioridade).Select(s => (int?)s.Horas).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return linha is null ? null : (linha.EquipeId, linha.HorasDeSla, linha.Ativa);
    }

    public async Task AdicionarAsync(CategoriaDeServico categoria, CancellationToken cancellationToken)
    {
        await _dbContext.CategoriasDeServico.AddAsync(categoria, cancellationToken);
    }

    public Task<CategoriaDeServico?> ObterParaEscritaAsync(Guid categoriaId, CancellationToken cancellationToken) =>
        _dbContext.CategoriasDeServico.SingleOrDefaultAsync(c => c.Id == categoriaId, cancellationToken);
}
