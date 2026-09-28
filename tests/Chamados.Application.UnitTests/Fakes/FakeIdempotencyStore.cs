using SharedKernel;
using SharedKernel.Messaging;

namespace Chamados.Application.UnitTests.Fakes;

/// <summary>
/// Fake in-memory de IIdempotencyStore com o contrato de IdempotencyRequest (arquitetura/06,
/// arquitetura/25). Separa o que foi só encenado (StageCompletionAsync) do que foi gravado:
/// a conclusão encenada só vira registro concluído quando o FakeUnitOfWork chama
/// GravarConclusoesPendentes, simulando o único SaveChanges do UnitOfWorkBehavior.
/// </summary>
public sealed class FakeIdempotencyStore : IIdempotencyStore
{
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, IdempotencyRecord> _registros = new();
    private readonly Dictionary<string, string> _conclusoesPendentes = new();

    public FakeIdempotencyStore(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public IReadOnlyDictionary<string, IdempotencyRecord> Registros => _registros;

    public int Liberacoes { get; private set; }

    private static string Chave(IdempotencyRequest request) => $"{request.Scope}|{request.Key}";

    public Task<IdempotencyRecord?> FindAsync(IdempotencyRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(_registros.GetValueOrDefault(Chave(request)));

    public Task ReserveAsync(IdempotencyRequest request, CancellationToken cancellationToken)
    {
        if (!_registros.TryAdd(
                Chave(request),
                new IdempotencyRecord(IsCompleted: false, request.PayloadHash, SerializedResponse: null, _timeProvider.GetUtcNow())))
        {
            throw new OperationInProgressException("Reserva concorrente.");
        }

        return Task.CompletedTask;
    }

    public Task<bool> TryRenewReservationAsync(
        IdempotencyRequest request, DateTimeOffset previousReservedAt, CancellationToken cancellationToken)
    {
        var atual = _registros[Chave(request)];
        if (atual.IsCompleted || atual.ReservedAt != previousReservedAt)
        {
            return Task.FromResult(false);
        }

        _registros[Chave(request)] = atual with { ReservedAt = _timeProvider.GetUtcNow() };
        return Task.FromResult(true);
    }

    public Task StageCompletionAsync(IdempotencyRequest request, string serializedResponse, CancellationToken cancellationToken)
    {
        _conclusoesPendentes[Chave(request)] = serializedResponse;
        return Task.CompletedTask;
    }

    public Task CompleteAsync(IdempotencyRequest request, string serializedResponse, CancellationToken cancellationToken)
    {
        Concluir(Chave(request), serializedResponse);
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(IdempotencyRequest request, CancellationToken cancellationToken)
    {
        _conclusoesPendentes.Remove(Chave(request));
        _registros.Remove(Chave(request));
        Liberacoes++;
        return Task.CompletedTask;
    }

    /// <summary>Chamado pelo FakeUnitOfWork no commit: grava as conclusões encenadas.</summary>
    internal void GravarConclusoesPendentes()
    {
        foreach (var (chave, resposta) in _conclusoesPendentes)
        {
            Concluir(chave, resposta);
        }

        _conclusoesPendentes.Clear();
    }

    private void Concluir(string chave, string serializedResponse) =>
        _registros[chave] = _registros[chave] with { IsCompleted = true, SerializedResponse = serializedResponse };
}
