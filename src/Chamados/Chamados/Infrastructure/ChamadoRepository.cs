using Chamados.Application;
using Chamados.Domain;
using Microsoft.EntityFrameworkCore;

namespace Chamados.Infrastructure;

/// <summary>
/// Implementação real (EF Core) de IChamadoRepository. Distinção leitura rastreada vs. não
/// rastreada, conforme XML doc da interface (13-estrategia-de-testes.md):
/// ObterParaEscritaAsync rastreia (tracking, usado quando o handler vai chamar um método de
/// ação e depois SalvarAsync); ObterSomenteLeituraAsync/ListarPor* não rastreiam (AsNoTracking,
/// arquitetura/27-leitura-e-query-side.md).
///
/// Concorrência otimista (plano-de-arquitetura.md secao 5, "Concorrência otimista";
/// arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md): ObterParaEscritaAsync
/// carrega a entidade rastreada com o valor de RowVersion atual do banco como OriginalValue do
/// EF Core (comportamento padrão do change tracker ao materializar uma linha). Se outra
/// transação alterar a mesma linha entre este load e o SaveChanges do UnitOfWorkBehavior, o
/// UPDATE gerado (WHERE Id = @Id AND RowVersion = @OriginalRowVersion) não afeta nenhuma linha
/// e o EF Core lança DbUpdateConcurrencyException, traduzida por ChamadosUnitOfWork.
///
/// Ciclo ETag/If-Match fechado (revisão de código + achado de smoke test manual,
/// testes-manuais.md): GET /chamados/{id} expõe RowVersion (ChamadoDetalheDto.RowVersion,
/// base64) como ETag; AtribuirChamadoCommand carrega o valor decodificado do If-Match como
/// RowVersion; ObterParaEscritaAsync, quando recebe esse valor, sobrescreve o OriginalValue do
/// change tracker com ele em vez do valor lido do banco — o UPDATE subsequente
/// (WHERE RowVersion=@valorDoCliente) rejeita a gravação se o ETag que o cliente enviou já
/// estava desatualizado, não só se duas requisições colidirem exatamente no meio do load.
/// Ainda não é um 412 Precondition Failed separado (RFC 7232) antes da escrita — é a mesma
/// UPDATE atômica de sempre, só que comparando contra o valor do cliente; resulta em 409
/// (ConcurrencyException) tanto para ETag desatualizado quanto para colisão real entre duas
/// requisições, sem gap de TOCTOU entre uma checagem separada e o commit.
/// </summary>
internal sealed class ChamadoRepository : IChamadoRepository
{
    private readonly ChamadosDbContext _dbContext;

    public ChamadoRepository(ChamadosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AdicionarAsync(Chamado chamado, CancellationToken cancellationToken)
    {
        await _dbContext.Chamados.AddAsync(chamado, cancellationToken);
    }

    public async Task<Chamado?> ObterParaEscritaAsync(
        Guid chamadoId, byte[]? rowVersionEsperado, CancellationToken cancellationToken)
    {
        var chamado = await _dbContext.Chamados
            .FirstOrDefaultAsync(c => c.Id == chamadoId, cancellationToken);

        if (chamado is not null && rowVersionEsperado is not null)
        {
            _dbContext.Entry(chamado).Property("RowVersion").OriginalValue = rowVersionEsperado;
        }

        return chamado;
    }

    public async Task<Chamado?> ObterSomenteLeituraAsync(Guid chamadoId, CancellationToken cancellationToken)
    {
        return await _dbContext.Chamados
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chamadoId, cancellationToken);
    }

    public async Task<byte[]?> ObterRowVersionAsync(Guid chamadoId, CancellationToken cancellationToken)
    {
        return await _dbContext.Chamados
            .AsNoTracking()
            .Where(c => c.Id == chamadoId)
            .Select(c => EF.Property<byte[]>(c, "RowVersion"))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Chamado>> ListarPorSolicitanteAsync(
        Guid solicitanteId, int page, int pageSize, CancellationToken cancellationToken)
    {
        return await _dbContext.Chamados
            .AsNoTracking()
            .Where(c => c.SolicitanteId == solicitanteId)
            .OrderByDescending(c => c.AbertoEm)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Chamado>> ListarPorEquipeAsync(
        Guid equipeId, int page, int pageSize, CancellationToken cancellationToken)
    {
        return await _dbContext.Chamados
            .AsNoTracking()
            .Where(c => c.EquipeId == equipeId)
            .OrderByDescending(c => c.AbertoEm)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task SalvarAsync(Chamado chamado, CancellationToken cancellationToken)
    {
        // Nada a encenar explicitamente: chamado já está rastreado (veio de ObterParaEscritaAsync)
        // e o change tracker do EF Core já capturou as mudanças feitas pelos métodos de ação do
        // agregado. O commit é único, no UnitOfWorkBehavior (arquitetura/25).
        return Task.CompletedTask;
    }
}
