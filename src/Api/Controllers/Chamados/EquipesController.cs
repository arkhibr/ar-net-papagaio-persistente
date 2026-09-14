using Api.Infrastructure;
using Api.Pagination;
using Asp.Versioning;
using Chamados.Contracts;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Chamados;

/// <summary>
/// GET /api/v1/equipes/fila — FilaDaEquipeQuery, paginada, não cacheável
/// (plano-de-arquitetura.md secao 5/6). Rota-base "equipes" (substantivo plural), sub-recurso
/// "fila": o plano descreve como GET /api/v1/equipes/fila diretamente (não uma ação de negócio
/// nomeada sobre um {id} — é leitura, não transição de estado), então segue como rota fixa em
/// vez de .../equipes/{id}/fila; a equipe do ator é resolvida no handler via
/// IEquipeDoUsuarioResolver, nunca aceita como parâmetro de rota vindo do cliente (filtro por
/// linha, mesma decisão documentada em FilaDaEquipeQuery).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/equipes")]
public sealed class EquipesController : ControllerBase
{
    private readonly ISender _sender;

    public EquipesController(ISender sender)
    {
        _sender = sender;
    }

    // Papel Técnico/Supervisor é leitura A (autorização por papel genérico, não depende do
    // conteúdo da Query): [Authorize(Roles=...)] nativo → 403 se faltar o papel, nunca
    // Result.Failure→400 (arquitetura/12, arquitetura/06). O filtro por linha (equipe do próprio
    // ator) fica no handler; os dois mecanismos coexistem.
    [HttpGet("fila")]
    [Authorize(Roles = "Tecnico,Supervisor")]
    public async Task<IActionResult> FilaAsync(
        [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var (paginaValida, tamanhoValido) = PaginacaoDefaults.Normalizar(page, pageSize);

        var resultado = await _sender.Send(
            new FilaDaEquipeQuery(paginaValida, tamanhoValido), cancellationToken);

        return resultado.ToActionResult(
            items => Ok(PagedResponse<ChamadoResumoDto>.DeListaSemContagemTotal(items, paginaValida, tamanhoValido)));
    }
}
