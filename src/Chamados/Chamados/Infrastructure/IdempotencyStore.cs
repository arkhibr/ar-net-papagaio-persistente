using Microsoft.EntityFrameworkCore;
using SharedKernel;
using SharedKernel.Messaging;

namespace Chamados.Infrastructure;

/// <summary>
/// IIdempotencyStore do módulo Chamados, no mesmo ChamadosDbContext (Scoped) do
/// ChamadosUnitOfWork. Só a reserva e a liberação gravam sozinhas; a conclusão é encenada e
/// vai no único SaveChanges do UnitOfWorkBehavior, junto com a mudança de negócio
/// (arquitetura/25, "Idempotência participa da mesma transação"; C2 de achados.md).
/// </summary>
internal sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly ChamadosDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public IdempotencyStore(ChamadosDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<IdempotencyRecord?> FindAsync(IdempotencyRequest request, CancellationToken cancellationToken)
    {
        var entity = await Registro(request)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null
            ? null
            : new IdempotencyRecord(entity.IsCompleted, entity.PayloadHash, entity.SerializedResponse, entity.ReservedAt);
    }

    public async Task ReserveAsync(IdempotencyRequest request, CancellationToken cancellationToken)
    {
        var entity = IdempotencyRecordEntity.Reserve(
            request.Scope, request.Key, request.PayloadHash, _timeProvider.GetUtcNow());

        _dbContext.IdempotencyRecords.Add(entity);

        try
        {
            // Commit próprio e imediato: a reserva precisa estar visível para uma requisição
            // concorrente antes do handler rodar (lacuna D1 de achados.md).
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.Entry(entity).State = EntityState.Detached;

            // Violação do índice único (Scope, IdempotencyKey): outra requisição reservou a mesma
            // chave entre o FindAsync e este INSERT. Qualquer outra falha de gravação propaga.
            if (await Registro(request).AsNoTracking().AnyAsync(CancellationToken.None))
            {
                throw new OperationInProgressException(
                    "Já existe uma operação em andamento para esta Idempotency-Key.");
            }

            throw;
        }
    }

    public async Task<bool> TryRenewReservationAsync(
        IdempotencyRequest request, DateTimeOffset previousReservedAt, CancellationToken cancellationToken)
    {
        // UPDATE condicional: só uma requisição assume a reserva expirada.
        var afetadas = await Registro(request)
            .Where(r => !r.IsCompleted && r.ReservedAt == previousReservedAt)
            .ExecuteUpdateAsync(r => r.SetProperty(x => x.ReservedAt, _timeProvider.GetUtcNow()), cancellationToken);

        return afetadas == 1;
    }

    public async Task StageCompletionAsync(
        IdempotencyRequest request, string serializedResponse, CancellationToken cancellationToken)
    {
        var entity = await Registro(request).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Não há reserva de idempotência a concluir para esta requisição.");

        entity.Complete(serializedResponse);
    }

    public async Task CompleteAsync(
        IdempotencyRequest request, string serializedResponse, CancellationToken cancellationToken)
    {
        await StageCompletionAsync(request, serializedResponse, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(IdempotencyRequest request, CancellationToken cancellationToken)
    {
        // Descarta o que o handler encenou (nada da tentativa que falhou pode ir junto) e apaga a
        // reserva direto no banco, sem passar pelo change tracker.
        _dbContext.ChangeTracker.Clear();

        await Registro(request)
            .Where(r => !r.IsCompleted)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private IQueryable<IdempotencyRecordEntity> Registro(IdempotencyRequest request) =>
        _dbContext.IdempotencyRecords.Where(r => r.Scope == request.Scope && r.IdempotencyKey == request.Key);
}
