using Catalogo.Contracts;
using SharedKernel;
using Mediator;

namespace Catalogo.Application.Queries;

/// <summary>Lista de categorias de serviço, só ativas ou todas (dado global, cacheado sem escopo de ator).</summary>
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
        var categorias = await _repository.ListarAsync(request.IncluirInativas, cancellationToken);

        return Result<IReadOnlyList<CategoriaDeServicoDto>>.Success(categorias);
    }
}
