using Catalogo.Contracts;
using SharedKernel;
using Mediator;

namespace Catalogo.Application.Queries;

/// <summary>
/// Lista todas as categorias de serviço cadastradas, convertidas para DTO de leitura.
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class CategoriasDeServicoQueryHandler
    : IRequestHandler<CategoriasDeServicoQuery, Result<IReadOnlyList<CategoriaDeServicoDto>>>
{
    private readonly ICategoriaDeServicoRepository _repository;

    public CategoriasDeServicoQueryHandler(ICategoriaDeServicoRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<Result<IReadOnlyList<CategoriaDeServicoDto>>> Handle(
        CategoriasDeServicoQuery request, CancellationToken cancellationToken)
    {
        var categorias = await _repository.ListarTodasAsync(cancellationToken);

        var dtos = categorias
            .Select(c => new CategoriaDeServicoDto(c.Id, c.Nome, c.EquipeId))
            .ToList();

        return Result<IReadOnlyList<CategoriaDeServicoDto>>.Success(dtos);
    }
}
