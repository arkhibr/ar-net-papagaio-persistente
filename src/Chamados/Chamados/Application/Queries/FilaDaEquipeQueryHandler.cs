using Chamados.Contracts;
using SharedKernel;
using Mediator;

namespace Chamados.Application.Queries;

/// <summary>
/// Resolve a EquipeId do próprio ator via IEquipeDoUsuarioResolver e lista via
/// IChamadoRepository.ListarPorEquipeAsync — nunca aceita EquipeId vinda do cliente (filtro
/// por linha, arquitetura/14-filtro-de-dados.md).
///
/// A checagem de papel (Técnico/Supervisor) NÃO mora aqui: é autorização por papel genérico,
/// sem depender do conteúdo da Query (leitura A, arquitetura/12), então é [Authorize(Roles=...)]
/// nativo no EquipesController (403), não Result.Failure (que viraria 400, semântica errada para
/// falta de permissão). O handler cuida só do filtro por linha, que é a via de acesso que o
/// atributo de papel não cobre — os dois mecanismos coexistem, nenhum substitui o outro.
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class FilaDaEquipeQueryHandler : IRequestHandler<FilaDaEquipeQuery, Result<IReadOnlyList<ChamadoResumoDto>>>
{
    private readonly IChamadoRepository _repository;
    private readonly IEquipeDoUsuarioResolver _equipeDoUsuarioResolver;
    private readonly ICurrentUser _currentUser;

    public FilaDaEquipeQueryHandler(
        IChamadoRepository repository, IEquipeDoUsuarioResolver equipeDoUsuarioResolver, ICurrentUser currentUser)
    {
        _repository = repository;
        _equipeDoUsuarioResolver = equipeDoUsuarioResolver;
        _currentUser = currentUser;
    }

    public async ValueTask<Result<IReadOnlyList<ChamadoResumoDto>>> Handle(
        FilaDaEquipeQuery request, CancellationToken cancellationToken)
    {
        var equipeId = await _equipeDoUsuarioResolver.ResolverEquipeIdAsync(_currentUser.UserId, cancellationToken);
        if (equipeId is null)
        {
            return Result<IReadOnlyList<ChamadoResumoDto>>.Failure(
                "Usuário autenticado não está vinculado a nenhuma equipe.");
        }

        var chamados = await _repository.ListarPorEquipeAsync(
            equipeId.Value, request.Page, request.PageSize, cancellationToken);

        var resumos = chamados
            .Select(c => new ChamadoResumoDto(
                c.Id, c.CategoriaId, c.EquipeId, c.Prioridade, c.Status, c.AbertoEm, c.PrazoSla,
                c.TecnicoAtribuidoId, c.Escalonado))
            .ToList();

        return Result<IReadOnlyList<ChamadoResumoDto>>.Success(resumos);
    }
}
