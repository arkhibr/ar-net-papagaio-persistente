using Microsoft.EntityFrameworkCore;
using SharedKernel.Messaging;

namespace Chamados.Infrastructure;

/// <summary>
/// Implementação real de IIdempotencyStore (SharedKernel.Messaging), no mesmo
/// ChamadosDbContext (Scoped por requisição) que ChamadosUnitOfWork/ChamadoRepository usam.
///
/// Divergência consciente da descrição de arquitetura/25-transacao-e-unit-of-work.md
/// ("a marcação de concluída... é commitada pelo mesmo SaveChanges do UnitOfWorkBehavior"),
/// registrada aqui porque o SharedKernel.Messaging.IdempotencyBehavior já implementado
/// (SharedKernel/Messaging/IdempotencyBehavior.cs, fora do escopo desta camada) chama
/// _store.CompleteAsync(...) DEPOIS de "await next(...)" já ter retornado — ou seja, depois
/// que o UnitOfWorkBehavior (dentro de next) já chamou SaveChangesAsync e já commitou (ou não)
/// a mudança de negócio. Não há como CompleteAsync "pegar carona" nesse SaveChanges: ele já
/// aconteceu antes de CompleteAsync ser chamado. Por isso, diferente do que o texto de
/// arquitetura/25 sugere, esta implementação faz SaveChangesAsync isolado em CADA método
/// (ReserveAsync, CompleteAsync, ReleaseAsync) — não há alternativa dentro do contrato real de
/// IdempotencyBehavior tal como já implementado e testado (SharedKernel.UnitTests).
/// Consequência aceita: a marcação de "concluída" e a mudança de negócio ficam em dois
/// SaveChanges (duas transações) diferentes, não uma só; existe uma janela pequena, entre os
/// dois commits, em que a operação de negócio já aconteceu mas ainda não está marcada como
/// concluída (se o processo cair exatamente nessa janela, um reenvio da mesma chave chamaria
/// o handler de novo). Ver Infrastructure.IntegrationTests (persistencia-e-integracao,
/// construcao-de-testes) como o lugar apropriado para decidir se essa janela é aceitável ou se
/// o pipeline (SharedKernel) precisa mudar para fechar essa lacuna.
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

    public async Task<IdempotencyRecord?> FindAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, cancellationToken);

        return entity is null
            ? null
            : new IdempotencyRecord(entity.IdempotencyKey, entity.IsCompleted, entity.SerializedResponse);
    }

    public async Task ReserveAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var entity = IdempotencyRecordEntity.Reservar(idempotencyKey, _timeProvider.GetUtcNow());

        await _dbContext.IdempotencyRecords.AddAsync(entity, cancellationToken);

        // SaveChanges isolado e imediato (não o do UnitOfWorkBehavior): a reserva precisa
        // ficar visível no banco, com o índice único fazendo cumprir a exclusão mútua, antes
        // do handler seguir em frente. Ver nota na doc da classe.
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CompleteAsync(
        string idempotencyKey, string serializedResponse, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, cancellationToken);

        if (entity is null)
        {
            // Não deveria acontecer no fluxo normal (ReserveAsync sempre roda antes), mas não é
            // papel deste store recriar uma reserva perdida - falha alto e cedo.
            throw new InvalidOperationException(
                $"Não há reserva de idempotência para a chave '{idempotencyKey}' a concluir.");
        }

        entity.Concluir(serializedResponse);

        // SaveChanges isolado e imediato: ver nota na doc da classe sobre por que não é
        // possível compartilhar o SaveChanges do UnitOfWorkBehavior aqui.
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, cancellationToken);

        if (entity is null)
        {
            return;
        }

        _dbContext.IdempotencyRecords.Remove(entity);

        // SaveChanges isolado e imediato (mesmo padrão de ReserveAsync/CompleteAsync).
        // ReleaseAsync roda no catch do IdempotencyBehavior, depois que uma exceção transitória
        // (ex.: ConcurrencyException) já abortou a tentativa de commit do UnitOfWorkBehavior
        // para esta requisição - sem um SaveChanges próprio aqui, a liberação da reserva se
        // perderia. O EF Core, após uma DbUpdateConcurrencyException, ainda permite chamar
        // SaveChanges novamente sobre o mesmo DbContext para outras entidades não relacionadas
        // ao conflito (aqui, IdempotencyRecordEntity não tem relação nenhuma com o agregado que
        // conflitou); a entidade do agregado que falhou permanece tracked com valores
        // divergentes, mas isso não impede este SaveChanges pontual sobre o registro de
        // idempotência.
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
