namespace Chamados.Application.UnitTests.Fakes;

/// <summary>
/// TimeProvider fake: nunca relógio real em teste (13-estrategia-de-testes.md,
/// "Testabilidade de tempo"). O handler consulta TimeProvider.GetUtcNow() e passa o
/// DateTimeOffset resolvido para o agregado, que nunca lê relógio por conta própria.
/// </summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _agora;

    public FixedTimeProvider(DateTimeOffset agora)
    {
        _agora = agora;
    }

    public override DateTimeOffset GetUtcNow() => _agora;
}
