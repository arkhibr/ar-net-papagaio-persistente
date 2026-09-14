using Catalogo.Application;

namespace Catalogo.Application.UnitTests.Fakes;

/// <summary>Fake in-memory de IMembroDeEquipeRepository.</summary>
internal sealed class FakeMembroDeEquipeRepository : IMembroDeEquipeRepository
{
    private readonly Dictionary<Guid, Guid> _equipePorTecnico = new();

    public FakeMembroDeEquipeRepository ComVinculo(Guid tecnicoId, Guid equipeId)
    {
        _equipePorTecnico[tecnicoId] = equipeId;
        return this;
    }

    public Task<bool> EhMembroAsync(Guid tecnicoId, Guid equipeId, CancellationToken cancellationToken) =>
        Task.FromResult(_equipePorTecnico.TryGetValue(tecnicoId, out var equipeVinculada) && equipeVinculada == equipeId);

    public Task<Guid?> ResolverEquipeIdAsync(Guid tecnicoId, CancellationToken cancellationToken) =>
        Task.FromResult(_equipePorTecnico.TryGetValue(tecnicoId, out var equipeId) ? equipeId : (Guid?)null);
}
