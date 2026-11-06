using Chamados.Application;
using Chamados.Contracts;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Chamados.Infrastructure;

/// <summary>
/// Leitura para exibição (arquitetura/27): AsNoTracking + projeção no banco, sem materializar o
/// agregado. As listagens contam com o mesmo filtro de linha da página (arquitetura/19).
/// </summary>
internal sealed class ChamadoLeitura : IChamadoLeitura
{
    private readonly ChamadosDbContext _dbContext;

    public ChamadoLeitura(ChamadosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ChamadoDetalheVersionado?> ObterDetalheAsync(Guid chamadoId, CancellationToken cancellationToken)
    {
        var linha = await _dbContext.Chamados
            .AsNoTracking()
            .Where(c => c.Id == chamadoId)
            .Select(c => new
            {
                Chamado = new ChamadoDetalheDto(
                    c.Id,
                    c.SolicitanteId,
                    c.CategoriaId,
                    c.EquipeId,
                    c.Prioridade,
                    c.Status,
                    c.AbertoEm,
                    c.PrazoSla,
                    c.TecnicoAtribuidoId,
                    c.NotaResolucao,
                    c.ResolvidoEm,
                    c.FechadoEm,
                    c.Escalonado,
                    c.DataEscalonamento),
                RowVersion = EF.Property<byte[]>(c, "RowVersion"),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return linha is null
            ? null
            : new ChamadoDetalheVersionado(linha.Chamado, Convert.ToBase64String(linha.RowVersion ?? []));
    }

    public Task<PagedResult<ChamadoResumoDto>> ListarPorSolicitanteAsync(
        Guid solicitanteId, int page, int pageSize, CancellationToken cancellationToken) =>
        PaginarAsync(_dbContext.Chamados.Where(c => c.SolicitanteId == solicitanteId), page, pageSize, cancellationToken);

    public Task<PagedResult<ChamadoResumoDto>> ListarPorEquipeAsync(
        Guid equipeId, int page, int pageSize, CancellationToken cancellationToken) =>
        PaginarAsync(_dbContext.Chamados.Where(c => c.EquipeId == equipeId), page, pageSize, cancellationToken);

    private static async Task<PagedResult<ChamadoResumoDto>> PaginarAsync(
        IQueryable<Domain.Chamado> filtrados, int page, int pageSize, CancellationToken cancellationToken)
    {
        var total = await filtrados.CountAsync(cancellationToken);

        var itens = await filtrados
            .AsNoTracking()
            .OrderByDescending(c => c.AbertoEm)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ChamadoResumoDto(
                c.Id, c.CategoriaId, c.EquipeId, c.Prioridade, c.Status, c.AbertoEm, c.PrazoSla,
                c.TecnicoAtribuidoId, c.Escalonado))
            .ToListAsync(cancellationToken);

        return new PagedResult<ChamadoResumoDto>(itens, total);
    }
}
