namespace SharedKernel.Messaging;

/// <summary>
/// Ponte, por requisição (Scoped), entre o IdempotencyBehavior (mais externo) e o
/// UnitOfWorkBehavior (mais interno): o primeiro registra como encenar a conclusão da chave;
/// o segundo encena antes do seu único SaveChanges, de modo que conclusão e mudança de negócio
/// são gravadas juntas (arquitetura/25). Indexado pela instância da mensagem, para que um
/// Command despachado dentro de outro nunca consuma a conclusão do externo.
/// </summary>
public sealed class PendingIdempotency
{
    private readonly Dictionary<object, Entry> _entries = new(ReferenceEqualityComparer.Instance);

    internal Entry Begin(object message, Func<object?, CancellationToken, Task> stage)
    {
        var entry = new Entry(stage);
        _entries[message] = entry;
        return entry;
    }

    internal void End(object message) => _entries.Remove(message);

    /// <summary>A conclusão pendente para esta mensagem, se ela passou pelo IdempotencyBehavior.</summary>
    public Entry? For(object message) => _entries.GetValueOrDefault(message);

    public sealed class Entry
    {
        private readonly Func<object?, CancellationToken, Task> _stage;

        internal Entry(Func<object?, CancellationToken, Task> stage)
        {
            _stage = stage;
        }

        public bool Committed { get; private set; }

        public Task StageAsync(object? response, CancellationToken cancellationToken) => _stage(response, cancellationToken);

        public void MarkCommitted() => Committed = true;
    }
}
