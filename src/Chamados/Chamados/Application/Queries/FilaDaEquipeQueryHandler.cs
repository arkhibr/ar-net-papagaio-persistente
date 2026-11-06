using Chamados.Contracts;
using SharedKernel;
using Mediator;

namespace Chamados.Application.Queries;

/// <summary>Fila da equipe do técnico/supervisor autenticado; a equipe vem do Catálogo, nunca da requisição.</summary>
internal sealed class FilaDaEquipeQueryHandler : IRequestHandler<FilaDaEquipeQuery, Result<PagedResult<ChamadoResumoDto>>>
{
    private readonly IChamadoLeitura _leitura;
    private readonly IEquipeDoUsuarioResolver _equipeDoUsuarioResolver;
    private readonly ICurrentUser _currentUser;

    public FilaDaEquipeQueryHandler(
        IChamadoLeitura leitura, IEquipeDoUsuarioResolver equipeDoUsuarioResolver, ICurrentUser currentUser)
    {
        _leitura = leitura;
        _equipeDoUsuarioResolver = equipeDoUsuarioResolver;
        _currentUser = currentUser;
    }

    public async ValueTask<Result<PagedResult<ChamadoResumoDto>>> Handle(
        FilaDaEquipeQuery request, CancellationToken cancellationToken)
    {
        var equipeId = await _equipeDoUsuarioResolver.ResolverEquipeIdAsync(_currentUser.UserId, cancellationToken);
        if (equipeId is null)
        {
            return Result<PagedResult<ChamadoResumoDto>>.Failure(
                "Usuário autenticado não está vinculado a nenhuma equipe.");
        }

        var pagina = await _leitura.ListarPorEquipeAsync(
            equipeId.Value, request.Page, request.PageSize, cancellationToken);

        return Result<PagedResult<ChamadoResumoDto>>.Success(pagina);
    }
}
