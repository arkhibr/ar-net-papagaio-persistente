using Api.Infrastructure;
using Asp.Versioning;
using Catalogo.Contracts;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Catalogo;

/// <summary>
/// Administração de equipes e membros (/api/v1/equipes), só Supervisor (leitura A). Divide a
/// rota-base com EquipesController (fila, módulo Chamados); os templates não se sobrepõem.
/// Desvincular é DELETE porque a remoção do vínculo é literalmente a semântica (arquitetura/09).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/equipes")]
[Authorize(Roles = PapeisDoCatalogo.Administrador)]
public sealed class CadastroDeEquipesController : ControllerBase
{
    private readonly ISender _sender;

    public CadastroDeEquipesController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>GET — equipes (por nome) com os ids dos membros.</summary>
    [HttpGet]
    public async Task<IActionResult> ListarAsync(CancellationToken cancellationToken) =>
        (await _sender.Send(new EquipesQuery(), cancellationToken)).ToActionResult();

    [HttpPost]
    public async Task<IActionResult> CriarAsync([FromBody] CriarEquipeRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _sender.Send(new CriarEquipeCommand(request.Nome), cancellationToken);
        return resultado.ToActionResult(id => Created($"{Request.Path}/{id}", new { id }));
    }

    [HttpPost("{id:guid}/renomear")]
    public async Task<IActionResult> RenomearAsync(Guid id, [FromBody] RenomearRequest request, CancellationToken cancellationToken) =>
        (await _sender.Send(new RenomearEquipeCommand(id, request.Nome), cancellationToken)).ToActionResult(_ => NoContent());

    /// <summary>Vincula um usuário (técnico ou supervisor). 400 se ele já estiver em outra equipe.</summary>
    [HttpPost("{id:guid}/membros")]
    public async Task<IActionResult> VincularAsync(Guid id, [FromBody] VincularMembroRequest request, CancellationToken cancellationToken) =>
        (await _sender.Send(new VincularMembroDaEquipeCommand(id, request.UsuarioId), cancellationToken)).ToActionResult(_ => NoContent());

    [HttpDelete("{id:guid}/membros/{usuarioId:guid}")]
    public async Task<IActionResult> DesvincularAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken) =>
        (await _sender.Send(new DesvincularMembroDaEquipeCommand(id, usuarioId), cancellationToken)).ToActionResult(_ => NoContent());
}

public sealed record CriarEquipeRequest(string Nome);

public sealed record VincularMembroRequest(Guid UsuarioId);
