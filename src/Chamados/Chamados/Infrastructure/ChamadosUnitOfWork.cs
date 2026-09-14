using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Chamados.Infrastructure;

/// <summary>
/// Implementação real de IUnitOfWork para o módulo Chamados. Um UnitOfWork por módulo (ver
/// nota em Catalogo/Infrastructure/CatalogoUnitOfWork.cs) — cada módulo é dono da própria
/// fronteira transacional, nunca compartilha DbContext/transação com outro módulo.
///
/// Único ponto da solução que menciona DbUpdateConcurrencyException pelo nome
/// (arquitetura/25-transacao-e-unit-of-work.md), traduzindo para ConcurrencyException
/// (SharedKernel) antes de propagar — nunca um Result.Failure, porque é falha transitória de
/// infraestrutura, não determinística (uma nova tentativa pode ter sucesso;
/// plano-de-arquitetura.md secao 5, "Concorrência otimista").
/// </summary>
internal sealed class ChamadosUnitOfWork : IUnitOfWork
{
    private readonly ChamadosDbContext _dbContext;

    public ChamadosUnitOfWork(ChamadosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("O recurso foi alterado por outra operação.", ex);
        }
    }
}
