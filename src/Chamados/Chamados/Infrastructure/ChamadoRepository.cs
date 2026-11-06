using Chamados.Application;
using Chamados.Domain;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Chamados.Infrastructure;

/// <summary>
/// Repositório de escrita (EF Core) de Chamado. Leitura para exibição fica em ChamadoLeitura.
///
/// Concorrência otimista (arquitetura/06, "ETag/If-Match"): com If-Match, a versão enviada é
/// comparada com a atual logo depois do carregamento e a divergência vira 412 antes de qualquer
/// regra de domínio. A versão do cliente também passa a ser o OriginalValue do change tracker,
/// então uma escrita concorrente entre o carregamento e o commit ainda é pega no UPDATE
/// (409, ConcurrencyException).
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
            var rowVersion = _dbContext.Entry(chamado).Property<byte[]>("RowVersion");

            if (!rowVersion.CurrentValue.AsSpan().SequenceEqual(rowVersionEsperado))
            {
                throw new PreconditionFailedException(
                    "O chamado foi alterado desde a última leitura. Obtenha o chamado de novo e repita a operação.");
            }

            rowVersion.OriginalValue = rowVersionEsperado;
        }

        return chamado;
    }

    public Task SalvarAsync(Chamado chamado, CancellationToken cancellationToken)
    {
        // Nada a encenar explicitamente: chamado já está rastreado (veio de ObterParaEscritaAsync)
        // e o change tracker do EF Core já capturou as mudanças feitas pelos métodos de ação do
        // agregado. O commit é único, no UnitOfWorkBehavior (arquitetura/25).
        return Task.CompletedTask;
    }
}
