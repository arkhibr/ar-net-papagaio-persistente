using Api.Infrastructure;
using Asp.Versioning;
using Catalogo.Contracts;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers.Catalogo;

/// <summary>
/// Categorias de serviço (/api/v1/categorias-de-servico). A listagem é para qualquer autenticado
/// (leitura A, [Authorize] simples), cacheada no pipeline e sem paginação: lista fechada e
/// pequena de referência (arquitetura/19 não exige envelope aqui).
///
/// Administração (Supervisor): criar e ações nomeadas por POST, nunca PUT genérico
/// (arquitetura/09). Sem Idempotency-Key: nenhuma destas ações tem efeito externo ou crítico
/// (critério de arquitetura/09). Uma categoria nunca é apagada, só inativada (B6 de achados.md).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/categorias-de-servico")]
public sealed class CategoriasDeServicoController : ControllerBase
{
    private const string Supervisor = PapeisDoCatalogo.Administrador;

    private readonly ISender _sender;

    public CategoriasDeServicoController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// GET — só as categorias ativas (as que aceitam chamado novo). incluirInativas=true é da
    /// administração: a própria Query recusa com 403 para quem não é Supervisor.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ListarAsync([FromQuery] bool incluirInativas, CancellationToken cancellationToken)
    {
        var resultado = await _sender.Send(new CategoriasDeServicoQuery(incluirInativas), cancellationToken);
        return resultado.ToActionResult();
    }

    /// <summary>POST — cria a categoria com a tabela de SLA. 201 com Location.</summary>
    [HttpPost]
    [Authorize(Roles = Supervisor)]
    public async Task<IActionResult> CriarAsync([FromBody] CriarCategoriaDeServicoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _sender.Send(
            new CriarCategoriaDeServicoCommand(request.Nome, request.EquipeId, request.Slas ?? []), cancellationToken);
        return resultado.ToActionResult(id => Created($"{Request.Path}/{id}", new { id }));
    }

    [HttpPost("{id:guid}/renomear")]
    [Authorize(Roles = Supervisor)]
    public async Task<IActionResult> RenomearAsync(Guid id, [FromBody] RenomearRequest request, CancellationToken cancellationToken) =>
        (await _sender.Send(new RenomearCategoriaDeServicoCommand(id, request.Nome), cancellationToken)).ToActionResult(_ => NoContent());

    /// <summary>Muda a equipe responsável. Chamados já abertos mantêm a equipe do snapshot.</summary>
    [HttpPost("{id:guid}/transferir")]
    [Authorize(Roles = Supervisor)]
    public async Task<IActionResult> TransferirAsync(Guid id, [FromBody] TransferirCategoriaRequest request, CancellationToken cancellationToken) =>
        (await _sender.Send(new TransferirCategoriaDeServicoCommand(id, request.EquipeId), cancellationToken)).ToActionResult(_ => NoContent());

    /// <summary>Substitui a tabela de SLA inteira; prioridade ausente deixa de ser atendida.</summary>
    [HttpPost("{id:guid}/definir-slas")]
    [Authorize(Roles = Supervisor)]
    public async Task<IActionResult> DefinirSlasAsync(Guid id, [FromBody] DefinirSlasRequest request, CancellationToken cancellationToken) =>
        (await _sender.Send(new DefinirSlasDaCategoriaCommand(id, request.Slas ?? []), cancellationToken)).ToActionResult(_ => NoContent());

    [HttpPost("{id:guid}/inativar")]
    [Authorize(Roles = Supervisor)]
    public async Task<IActionResult> InativarAsync(Guid id, CancellationToken cancellationToken) =>
        (await _sender.Send(new InativarCategoriaDeServicoCommand(id), cancellationToken)).ToActionResult(_ => NoContent());

    [HttpPost("{id:guid}/reativar")]
    [Authorize(Roles = Supervisor)]
    public async Task<IActionResult> ReativarAsync(Guid id, CancellationToken cancellationToken) =>
        (await _sender.Send(new ReativarCategoriaDeServicoCommand(id), cancellationToken)).ToActionResult(_ => NoContent());
}

/// <summary>Corpo de POST /categorias-de-servico. Campos em camelCase (arquitetura/09).</summary>
public sealed record CriarCategoriaDeServicoRequest(string Nome, Guid EquipeId, IReadOnlyList<SlaDto>? Slas);

/// <summary>Corpo de .../renomear (categoria ou equipe).</summary>
public sealed record RenomearRequest(string Nome);

public sealed record TransferirCategoriaRequest(Guid EquipeId);

public sealed record DefinirSlasRequest(IReadOnlyList<SlaDto>? Slas);
