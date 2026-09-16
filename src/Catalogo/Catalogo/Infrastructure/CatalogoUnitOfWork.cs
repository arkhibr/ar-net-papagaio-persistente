using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Catalogo.Infrastructure;

/// <summary>
/// Implementação real de IUnitOfWork para o módulo Catalogo. Um UnitOfWork por módulo é o
/// padrão mais simples com um DbContext por módulo (decisão registrada aqui, ver também
/// ChamadosUnitOfWork.cs): cada módulo é dono da própria fronteira transacional, nunca
/// compartilha DbContext/transação com outro módulo (arquitetura/04, arquitetura/25).
///
/// Catalogo não tem, nesta rodada, nenhum Command transacional (só Queries — ver
/// plano-de-arquitetura.md secao 5, tabela de Commands/Queries: nenhum Command aparece para
/// Catalogo). Por isso, na prática, SaveChangesAsync nunca é chamado hoje (Query não passa
/// pelo UnitOfWorkBehavior, arquitetura/25).
///
/// NÃO registrada em CatalogoDependencyInjection (ver nota lá): registrar
/// AddScoped&lt;IUnitOfWork, CatalogoUnitOfWork&gt;() colidiria silenciosamente com o registro
/// equivalente de ChamadosUnitOfWork quando os dois módulos estiverem na mesma composição raiz
/// (Api). Esta classe continua implementada e pronta para quando Catalogo ganhar seu primeiro
/// Command de escrita (ex.: cadastro de CategoriaDeServico/MembroDeEquipe) — nesse momento a
/// colisão de DI vira um problema real, resolvida por keyed services
/// (AddKeyedScoped&lt;IUnitOfWork&gt;), não implementado agora por não haver consumidor.
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
            foreach (var entry in ex.Entries)
            {
                entry.State = EntityState.Detached;
            }

            throw new ConcurrencyException("O recurso foi alterado por outra operação.", ex);
        }
    }
}
