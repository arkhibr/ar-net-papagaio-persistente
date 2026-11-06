using Catalogo.Contracts;
using Mediator;
using SharedKernel;

namespace Catalogo.Application.Queries;

/// <summary>Equipes com os membros, para a administração do Catálogo.</summary>
internal sealed class EquipesQueryHandler : IRequestHandler<EquipesQuery, Result<IReadOnlyList<EquipeDto>>>
{
    private readonly IEquipeRepository _equipes;

    public EquipesQueryHandler(IEquipeRepository equipes)
    {
        _equipes = equipes;
    }

    public async ValueTask<Result<IReadOnlyList<EquipeDto>>> Handle(EquipesQuery request, CancellationToken cancellationToken) =>
        Result<IReadOnlyList<EquipeDto>>.Success(await _equipes.ListarComMembrosAsync(cancellationToken));
}
