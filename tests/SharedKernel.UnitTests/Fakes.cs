using SharedKernel.Modules;

namespace SharedKernel.UnitTests;

internal sealed class FakeCurrentUser : ICurrentUser
{
    public Guid UserId { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public bool IsAuthenticated { get; init; } = true;
    public bool IsSystemActor { get; init; }
    public bool IsInRole(string role) => false;
}

/// <summary>IModuleService que sempre devolve a mesma implementação (teste de um módulo só).</summary>
internal sealed class FixedModuleService<T> : IModuleService<T>
    where T : notnull
{
    private readonly T _instancia;

    public FixedModuleService(T instancia)
    {
        _instancia = instancia;
    }

    public T For(Type messageType) => _instancia;
}

internal sealed class FixedTimeProvider : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Agora;
}
