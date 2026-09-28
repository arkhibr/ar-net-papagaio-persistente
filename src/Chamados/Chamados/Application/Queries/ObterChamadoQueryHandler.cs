using Chamados.Contracts;
using SharedKernel;
using Mediator;

namespace Chamados.Application.Queries;

/// <summary>
/// Detalhe de um chamado. A autorização já rodou no pipeline (ObterChamadoQuery
/// .IsAuthorizedAsync), então aqui só existe a leitura, projetada no banco junto com a versão.
/// </summary>
internal sealed class ObterChamadoQueryHandler : IRequestHandler<ObterChamadoQuery, Result<ChamadoDetalheVersionado>>
{
    private readonly IChamadoLeitura _leitura;

    public ObterChamadoQueryHandler(IChamadoLeitura leitura)
    {
        _leitura = leitura;
    }

    public async ValueTask<Result<ChamadoDetalheVersionado>> Handle(ObterChamadoQuery request, CancellationToken cancellationToken)
    {
        var detalhe = await _leitura.ObterDetalheAsync(request.ChamadoId, cancellationToken);

        return detalhe is null
            ? Result<ChamadoDetalheVersionado>.NotFound("Chamado não encontrado.")
            : Result<ChamadoDetalheVersionado>.Success(detalhe);
    }
}
