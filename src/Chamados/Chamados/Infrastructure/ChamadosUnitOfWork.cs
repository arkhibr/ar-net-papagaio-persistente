using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using SharedKernel.Domain;

namespace Chamados.Infrastructure;

/// <summary>
/// IUnitOfWork do módulo Chamados, registrado keyed pela chave do módulo. Cada módulo é dono da
/// própria fronteira transacional.
///
/// Antes do único SaveChanges, materializa um RegistroAuditoria para cada evento de domínio
/// IAuditable levantado pelos agregados rastreados, com o ator de ICurrentUser, na mesma
/// transação da mudança (arquitetura/21, "Atomicidade da gravação"; A5 de achados.md). Sem
/// ISaveChangesInterceptor: a materialização fica explícita aqui.
///
/// Único ponto que nomeia DbUpdateConcurrencyException (arquitetura/25), traduzida para
/// ConcurrencyException: falha transitória, nunca Result.Failure.
/// </summary>
internal sealed class ChamadosUnitOfWork : IUnitOfWork
{
    private readonly ChamadosDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public ChamadosUnitOfWork(ChamadosDbContext dbContext, ICurrentUser currentUser, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        MaterializarAuditoria();

        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("O recurso foi alterado por outra operação.", ex);
        }
    }

    public void DiscardChanges() => _dbContext.ChangeTracker.Clear();

    private void MaterializarAuditoria()
    {
        var agregados = _dbContext.ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(agregado => agregado.DomainEvents.Count > 0)
            .ToList();

        if (agregados.Count == 0)
        {
            return;
        }

        var agora = _timeProvider.GetUtcNow();
        var correlationId = Activity.Current?.TraceId.ToString();

        foreach (var agregado in agregados)
        {
            foreach (var evento in agregado.DomainEvents.OfType<IAuditable>())
            {
                _dbContext.RegistrosAuditoria.Add(RegistroAuditoriaEntity.De(evento, _currentUser, agora, correlationId));
            }

            agregado.ClearDomainEvents();
        }
    }
}
