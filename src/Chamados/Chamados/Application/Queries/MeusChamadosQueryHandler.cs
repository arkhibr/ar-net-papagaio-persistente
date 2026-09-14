using Chamados.Contracts;
using SharedKernel;
using Mediator;

namespace Chamados.Application.Queries;

/// <summary>
/// Resolve _currentUser.UserId e chama IChamadoRepository.ListarPorSolicitanteAsync com ele —
/// nunca aceita um SolicitanteId vindo do próprio Command (filtro por linha).
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class MeusChamadosQueryHandler : IRequestHandler<MeusChamadosQuery, Result<IReadOnlyList<ChamadoResumoDto>>>
{
    private readonly IChamadoRepository _repository;
    private readonly ICurrentUser _currentUser;

    public MeusChamadosQueryHandler(IChamadoRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async ValueTask<Result<IReadOnlyList<ChamadoResumoDto>>> Handle(
        MeusChamadosQuery request, CancellationToken cancellationToken)
    {
        var chamados = await _repository.ListarPorSolicitanteAsync(
            _currentUser.UserId, request.Page, request.PageSize, cancellationToken);

        var resumos = chamados
            .Select(c => new ChamadoResumoDto(
                c.Id, c.CategoriaId, c.EquipeId, c.Prioridade, c.Status, c.AbertoEm, c.PrazoSla,
                c.TecnicoAtribuidoId, c.Escalonado))
            .ToList();

        return Result<IReadOnlyList<ChamadoResumoDto>>.Success(resumos);
    }
}
