using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Catalogo.Infrastructure;

/// <summary>
/// Implementação real de IUnitOfWork para o módulo Catalogo. Um UnitOfWork por módulo é o
/// padrão mais simples com um DbContext por módulo (decisão registrada aqui, ver também
/// ChamadosUnitOfWork.cs): cada módulo é dono da própria fronteira transacional, nunca
/// compartilha DbContext/transação com outro módulo (arquitetura/04, arquitetura/25).
///
/// Usado pelos Commands de administração do Catálogo (categorias, SLAs, equipes e membros), todos
/// ITransactionalCommand: o UnitOfWorkBehavior chama SaveChangesAsync uma vez, depois do handler.
///
/// Registrada keyed pela chave do módulo (CatalogoDependencyInjection), sem colidir com
/// ChamadosUnitOfWork.
///
/// Único ponto da solução, dentro deste módulo, que menciona DbUpdateConcurrencyException
/// pelo nome (arquitetura/25-transacao-e-unit-of-work.md) — mesmo sem nenhuma entidade com
/// RowVersion em Catalogo ainda, mantém o mesmo padrão de tradução do módulo Chamados para
/// não haver dois caminhos diferentes de tratamento de concorrência na solution.
/// </summary>
internal sealed class CatalogoUnitOfWork : IUnitOfWork
{
    private readonly CatalogoDbContext _dbContext;

    public CatalogoUnitOfWork(CatalogoDbContext dbContext)
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
            // Mesmo detach de ChamadosUnitOfWork.SaveChangesAsync (ver nota lá) — mantém os dois
            // módulos com o mesmo tratamento de concorrência, mesmo sem consumidor real ainda.
            throw new ConcurrencyException("O recurso foi alterado por outra operação.", ex);
        }
    }

    public void DiscardChanges() => _dbContext.ChangeTracker.Clear();
}
