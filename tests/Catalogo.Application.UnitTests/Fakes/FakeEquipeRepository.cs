using Catalogo.Application;
using Catalogo.Contracts;
using Catalogo.Domain;

namespace Catalogo.Application.UnitTests.Fakes;

/// <summary>Fake in-memory de IEquipeRepository.</summary>
internal sealed class FakeEquipeRepository : IEquipeRepository
{
    private readonly Dictionary<Guid, Equipe> _equipes = new();

    public FakeEquipeRepository ComEquipe(Equipe equipe)
    {
        _equipes[equipe.Id] = equipe;
        return this;
    }

    public Task<IReadOnlyList<EquipeDto>> ListarComMembrosAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EquipeDto>>(_equipes.Values.Select(e => new EquipeDto(e.Id, e.Nome, [])).ToList());

    public Task<bool> ExisteAsync(Guid equipeId, CancellationToken cancellationToken) =>
        Task.FromResult(_equipes.ContainsKey(equipeId));

    public Task AdicionarAsync(Equipe equipe, CancellationToken cancellationToken)
    {
        _equipes[equipe.Id] = equipe;
        return Task.CompletedTask;
    }

    public Task<Equipe?> ObterParaEscritaAsync(Guid equipeId, CancellationToken cancellationToken) =>
        Task.FromResult(_equipes.GetValueOrDefault(equipeId));
}

/// <summary>Registra as chaves invalidadas.</summary>
internal sealed class FakeCacheInvalidator : SharedKernel.ICacheInvalidator
{
    public List<string> Invalidadas { get; } = [];

    public void InvalidarGlobal<TQuery>(string cacheKey) where TQuery : SharedKernel.IGlobalCacheableQuery =>
        Invalidadas.Add(cacheKey);
}
