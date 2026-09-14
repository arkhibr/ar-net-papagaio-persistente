using Catalogo.Domain;

namespace Catalogo.Application;

/// <summary>
/// Porta de repositório do agregado CategoriaDeServico. Implementação real (EF Core) mora
/// na Infrastructure, fora do escopo de Application.UnitTests (persistencia-e-integracao).
/// ListarTodasAsync é leitura pura (no-tracking na implementação real).
/// </summary>
internal interface ICategoriaDeServicoRepository
{
    Task<IReadOnlyList<CategoriaDeServico>> ListarTodasAsync(CancellationToken cancellationToken);

    Task<CategoriaDeServico?> ObterPorIdAsync(Guid categoriaId, CancellationToken cancellationToken);
}
