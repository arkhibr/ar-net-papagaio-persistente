using SharedKernel;

namespace Chamados.Application.UnitTests.Fakes;

/// <summary>
/// Fake de IUnitOfWork (arquitetura/25-transacao-e-unit-of-work.md). No SaveChanges grava as
/// conclusões de idempotência encenadas no FakeIdempotencyStore; uma exceção configurada via
/// FalharNoProximoSaveCom simula falha transitória de infraestrutura (ex.: conflito de gravação
/// otimista) vinda do commit.
/// </summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly FakeIdempotencyStore _idempotencyStore;
    private Exception? _falhaNoProximoSave;

    public FakeUnitOfWork(FakeIdempotencyStore idempotencyStore)
    {
        _idempotencyStore = idempotencyStore;
    }

    public int Commits { get; private set; }
    public int Descartes { get; private set; }

    public FakeUnitOfWork FalharNoProximoSaveCom(Exception exception)
    {
        _falhaNoProximoSave = exception;
        return this;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (_falhaNoProximoSave is { } falha)
        {
            _falhaNoProximoSave = null;
            throw falha;
        }

        _idempotencyStore.GravarConclusoesPendentes();
        Commits++;
        return Task.FromResult(1);
    }

    public void DiscardChanges() => Descartes++;
}
