using Api.Infrastructure;
using Asp.Versioning;
using Catalogo.Contracts;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Catalogo;

/// <summary>
/// GET /api/v1/categorias-de-servico — CategoriasDeServicoQuery. Autorização: qualquer
/// autenticado (leitura A, [Authorize] simples — CategoriasDeServicoQuery não implementa
/// IRequiresAuthorization, plano-de-arquitetura.md secao 5). Cacheável no pipeline
/// (ICacheableQuery), sem paginação: lista fechada e pequena de categorias de referência
/// (nenhuma paginação pedida para esta rota no plano — arquitetura/19-paginacao.md não exige
/// envelope quando o próprio recurso não é uma listagem que cresce sem limite).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/categorias-de-servico")]
public sealed class CategoriasDeServicoController : ControllerBase
{
    private readonly ISender _sender;

    public CategoriasDeServicoController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ListarAsync(CancellationToken cancellationToken)
    {
        var resultado = await _sender.Send(new CategoriasDeServicoQuery(), cancellationToken);

        return resultado.ToActionResult();
    }
}
