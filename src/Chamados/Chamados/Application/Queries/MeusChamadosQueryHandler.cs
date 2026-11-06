using Chamados.Contracts;
using SharedKernel;
using Mediator;

namespace Chamados.Application.Queries;

/// <summary>Chamados do solicitante autenticado; o filtro por linha vem de ICurrentUser, nunca da requisição.</summary>
internal sealed class MeusChamadosQueryHandler : IRequestHandler<MeusChamadosQuery, Result<PagedResult<ChamadoResumoDto>>>
{
    private readonly IChamadoLeitura _leitura;
    private readonly ICurrentUser _currentUser;

    public MeusChamadosQueryHandler(IChamadoLeitura leitura, ICurrentUser currentUser)
    {
        _leitura = leitura;
        _currentUser = currentUser;
    }

    public async ValueTask<Result<PagedResult<ChamadoResumoDto>>> Handle(
        MeusChamadosQuery request, CancellationToken cancellationToken)
    {
        var pagina = await _leitura.ListarPorSolicitanteAsync(
            _currentUser.UserId, request.Page, request.PageSize, cancellationToken);

        return Result<PagedResult<ChamadoResumoDto>>.Success(pagina);
    }
}
