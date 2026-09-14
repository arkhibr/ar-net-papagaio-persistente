using SharedKernel;

namespace Chamados.Application.UnitTests.Fakes;

/// <summary>
/// ICurrentUser fake para os cenários de autorização por recurso (13-estrategia-de-testes.md).
/// Nunca HttpContext real.
/// </summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase);

    public Guid UserId { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public bool IsAuthenticated { get; init; } = true;
    public bool IsSystemActor { get; init; }

    public FakeCurrentUser ComPapel(string role)
    {
        _roles.Add(role);
        return this;
    }

    public bool IsInRole(string role) => _roles.Contains(role);

    public static FakeCurrentUser SistemaAutomatizado() => new() { IsSystemActor = true };
}
