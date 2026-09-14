using Catalogo.Application;
using Catalogo.Domain;

namespace Catalogo.Application.UnitTests.Fakes;

/// <summary>Fake in-memory de ICategoriaDeServicoRepository.</summary>
internal sealed class FakeCategoriaDeServicoRepository : ICategoriaDeServicoRepository
{
    private readonly Dictionary<Guid, CategoriaDeServico> _categorias = new();

    public FakeCategoriaDeServicoRepository ComCategoria(CategoriaDeServico categoria)
    {
        _categorias[categoria.Id] = categoria;
        return this;
    }

    public Task<IReadOnlyList<CategoriaDeServico>> ListarTodasAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CategoriaDeServico>>(_categorias.Values.ToList());

    public Task<CategoriaDeServico?> ObterPorIdAsync(Guid categoriaId, CancellationToken cancellationToken) =>
        Task.FromResult(_categorias.GetValueOrDefault(categoriaId));
}
