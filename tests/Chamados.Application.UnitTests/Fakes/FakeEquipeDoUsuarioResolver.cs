using Chamados.Application;

namespace Chamados.Application.UnitTests.Fakes;

public sealed class FakeEquipeDoUsuarioResolver : IEquipeDoUsuarioResolver
{
    private readonly Dictionary<Guid, Guid> _equipePorUsuario = new();

    public FakeEquipeDoUsuarioResolver ComEquipe(Guid userId, Guid equipeId)
    {
        _equipePorUsuario[userId] = equipeId;
        return this;
    }

    public Task<Guid?> ResolverEquipeIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_equipePorUsuario.TryGetValue(userId, out var equipeId) ? equipeId : (Guid?)null);
}
