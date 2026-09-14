using SharedKernel.Messaging;

namespace Chamados.Application.UnitTests.Fakes;

/// <summary>Fake in-memory de IIdempotencyStore, usado pelos testes de handler que envolvem Commands idempotentes.</summary>
public sealed class FakeIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<string, IdempotencyRecord> _registros = new();

    public Task<IdempotencyRecord?> FindAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(_registros.GetValueOrDefault(idempotencyKey));

    public Task ReserveAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        _registros[idempotencyKey] = new IdempotencyRecord(idempotencyKey, IsCompleted: false, SerializedResponse: null);
        return Task.CompletedTask;
    }

    public Task CompleteAsync(string idempotencyKey, string serializedResponse, CancellationToken cancellationToken)
    {
        _registros[idempotencyKey] = new IdempotencyRecord(idempotencyKey, IsCompleted: true, serializedResponse);
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        _registros.Remove(idempotencyKey);
        return Task.CompletedTask;
    }
}
