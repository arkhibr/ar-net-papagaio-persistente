using Api.Infrastructure;
using Api.Pagination;
using Asp.Versioning;
using Chamados.Contracts;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.Controllers.Chamados;

/// <summary>
/// Controller fino do módulo Chamados (arquitetura/09-convencoes-rest-api.md,
/// plano-de-arquitetura.md secao 6): só traduz requisição HTTP em Command/Query via
/// ISender.Send(...) e Result&lt;T&gt; em resposta HTTP, sem lógica de negócio, sem DbContext,
/// sem decisão sobre dado de mais de um módulo. Rota-base "chamados" (substantivo plural em
/// português, língua de domínio, arquitetura/08-convencao-de-nomenclatura.md).
///
/// EscalonarChamadoCommand NUNCA tem action aqui: só disparado pelo job no Worker
/// (plano-de-arquitetura.md secao 3/5/6) — decisão deliberada de não expor rota, não omissão.
///
/// Decisão sobre id da rota vs. corpo (não 100% explícita no plano, registrada aqui): toda
/// action de transição usa {id} da rota como o ChamadoId do Command — nunca duplicado no corpo
/// da requisição (um único "id" por operação, sem checagem de consistência rota-vs-corpo a
/// fazer, e sem o cliente poder mandar um ChamadoId divergente por engano). Os únicos dois
/// campos "escondidos" do cliente e resolvidos pelo controller, nunca aceitos como entrada
/// direta, são SolicitanteId (sempre de ICurrentUser.UserId ou, em Fechar/Reabrir, do próprio
/// Chamado já carregado — ver FecharAsync/ReabrirAsync) e RowVersion em Atribuir (do header
/// If-Match, nunca do corpo — arquitetura/06, "ETag/If-Match é a superfície HTTP do mesmo
/// RowVersion").
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chamados")]
public sealed class ChamadosController : ControllerBase
{
    private readonly ISender _sender;

    public ChamadosController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// POST /api/v1/chamados — AbrirChamadoCommand. Autorização: papel "solicitante
    /// autenticado", leitura A genérica ([Authorize] simples, plano-de-arquitetura.md secao 5;
    /// AbrirChamadoCommand não implementa IRequiresAuthorization por isso). Idempotency-Key
    /// obrigatória via header (arquitetura/09-convencoes-rest-api.md, "nunca no corpo") — sua
    /// ausência é erro do cliente (400), nunca gera chave nova no servidor silenciosamente.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AbrirAsync(
        [FromBody] AbrirChamadoRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return IdempotencyKeyAusente();
        }

        var command = new AbrirChamadoCommand(
            SolicitanteIdAtual, request.CategoriaId, request.Prioridade, idempotencyKey);

        var resultado = await _sender.Send(command, cancellationToken);

        return resultado.ToActionResult(
            id => CreatedAtAction(nameof(ObterAsync), new { id }, new { id }));
    }

    /// <summary>
    /// GET /api/v1/chamados/{id} — ObterChamadoQuery. Guard de autorização dentro do handler
    /// (ver ObterChamadoQuery).
    ///
    /// ETag (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md): a resposta
    /// inclui o header ETag com ChamadoDetalheDto.RowVersion (opaco, base64), fechando o ciclo
    /// que faltava para AtribuirAsync — o cliente lê o ETag aqui e devolve via If-Match sem
    /// nunca interpretar o valor. Achado de revisão de código + testes-manuais.md; antes desta
    /// correção, AtribuirAsync exigia If-Match sem nenhum GET fornecer um valor legítimo.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _sender.Send(new ObterChamadoQuery(id), cancellationToken);

        if (resultado.IsSuccess)
        {
            Response.Headers.ETag = $"\"{resultado.Value!.RowVersion}\"";
        }

        return resultado.ToActionResult();
    }

    /// <summary>
    /// GET /api/v1/chamados/meus — MeusChamadosQuery, paginada (arquitetura/19-paginacao.md).
    /// SolicitanteId nunca aceito por query string: o próprio handler resolve de
    /// ICurrentUser.UserId (filtro por linha).
    /// </summary>
    [HttpGet("meus")]
    [Authorize]
    public async Task<IActionResult> MeusAsync(
        [FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var (paginaValida, tamanhoValido) = PaginacaoDefaults.Normalizar(page, pageSize);

        var resultado = await _sender.Send(
            new MeusChamadosQuery(paginaValida, tamanhoValido), cancellationToken);

        return resultado.ToActionResult(
            items => Ok(PagedResponse<ChamadoResumoDto>.DeListaSemContagemTotal(items, paginaValida, tamanhoValido)));
    }

    /// <summary>
    /// POST /api/v1/chamados/{id}/atribuir — AtribuirChamadoCommand. Idempotency-Key +
    /// If-Match/RowVersion (plano-de-arquitetura.md secao 6: "único ponto de disputa
    /// concorrente descrita"). RowVersion vem do header If-Match (opaco, base64 do shadow
    /// property EF Core), nunca de um campo no corpo (arquitetura/06).
    /// </summary>
    [HttpPost("{id:guid}/atribuir")]
    [Authorize]
    public async Task<IActionResult> AtribuirAsync(
        Guid id,
        [FromBody] AtribuirChamadoRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return IdempotencyKeyAusente();
        }

        if (string.IsNullOrWhiteSpace(ifMatch) || !TentarDecodificarRowVersion(ifMatch, out var rowVersion))
        {
            return Problem(
                title: "Cabeçalho If-Match ausente ou inválido",
                detail: "A atribuição de um chamado exige o cabeçalho If-Match com o ETag obtido em GET /api/v1/chamados/{id}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new AtribuirChamadoCommand(id, request.TecnicoId, rowVersion, idempotencyKey);

        var resultado = await _sender.Send(command, cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>POST /api/v1/chamados/{id}/devolver — DevolverChamadoCommand.</summary>
    [HttpPost("{id:guid}/devolver")]
    [Authorize]
    public async Task<IActionResult> DevolverAsync(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return IdempotencyKeyAusente();
        }

        var resultado = await _sender.Send(new DevolverChamadoCommand(id, idempotencyKey), cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>POST /api/v1/chamados/{id}/reclassificar — ReclassificarChamadoCommand.</summary>
    [HttpPost("{id:guid}/reclassificar")]
    [Authorize]
    public async Task<IActionResult> ReclassificarAsync(
        Guid id,
        [FromBody] ReclassificarChamadoRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return IdempotencyKeyAusente();
        }

        var command = new ReclassificarChamadoCommand(id, request.NovaPrioridade, idempotencyKey);

        var resultado = await _sender.Send(command, cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>POST /api/v1/chamados/{id}/resolver — ResolverChamadoCommand.</summary>
    [HttpPost("{id:guid}/resolver")]
    [Authorize]
    public async Task<IActionResult> ResolverAsync(
        Guid id,
        [FromBody] ResolverChamadoRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return IdempotencyKeyAusente();
        }

        var command = new ResolverChamadoCommand(id, request.NotaResolucao, idempotencyKey);

        var resultado = await _sender.Send(command, cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>
    /// POST /api/v1/chamados/{id}/fechar — FecharChamadoCommand. FecharChamadoCommand exige
    /// SolicitanteId no próprio Command (comparado contra ICurrentUser.UserId OU
    /// IsSystemActor dentro de IsAuthorizedAsync — ver FecharChamadoCommandAuthorizationTests).
    /// O controller NUNCA aceita esse SolicitanteId do cliente (um cliente malicioso poderia
    /// mandar o SolicitanteId de outra pessoa e se autorizar): carrega o Chamado via
    /// ObterChamadoQuery primeiro para descobrir o SolicitanteId real do agregado, e só então
    /// monta o Command. Custo extra de uma leitura, aceito pela mesma razão que
    /// ObterChamadoQueryHandler já teria barrado um não-solicitante tentando espiar o chamado.
    /// </summary>
    [HttpPost("{id:guid}/fechar")]
    [Authorize]
    public async Task<IActionResult> FecharAsync(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return IdempotencyKeyAusente();
        }

        var chamado = await _sender.Send(new ObterChamadoQuery(id), cancellationToken);
        if (chamado.IsFailure)
        {
            return chamado.ToActionResult();
        }

        var command = new FecharChamadoCommand(id, chamado.Value!.SolicitanteId, idempotencyKey);

        var resultado = await _sender.Send(command, cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>
    /// POST /api/v1/chamados/{id}/reabrir — ReabrirChamadoCommand. Mesma decisão de FecharAsync:
    /// SolicitanteId nunca vem do cliente, sempre resolvido a partir do Chamado carregado.
    /// </summary>
    [HttpPost("{id:guid}/reabrir")]
    [Authorize]
    public async Task<IActionResult> ReabrirAsync(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return IdempotencyKeyAusente();
        }

        var chamado = await _sender.Send(new ObterChamadoQuery(id), cancellationToken);
        if (chamado.IsFailure)
        {
            return chamado.ToActionResult();
        }

        var command = new ReabrirChamadoCommand(id, chamado.Value!.SolicitanteId, idempotencyKey);

        var resultado = await _sender.Send(command, cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>
    /// SolicitanteId resolvido de ICurrentUser via HttpContext.User (claims), nunca do corpo da
    /// requisição (arquitetura/11-autenticacao-e-sessao.md). [Authorize] garante
    /// HttpContext.User autenticado antes desta property ser lida.
    /// </summary>
    private Guid SolicitanteIdAtual =>
        Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : Guid.Empty;

    private ObjectResult IdempotencyKeyAusente() => Problem(
        title: "Cabeçalho Idempotency-Key ausente",
        detail: "Esta operação exige o cabeçalho Idempotency-Key (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md).",
        statusCode: StatusCodes.Status400BadRequest);

    private static bool TentarDecodificarRowVersion(string ifMatch, out byte[] rowVersion)
    {
        try
        {
            // ETag convencional: aspas ao redor do valor opaco (RFC 9110). O valor em si é o
            // RowVersion (shadow property EF Core) codificado em base64 — opaco para o cliente,
            // nunca interpretado por ele (arquitetura/06).
            var valor = ifMatch.Trim('"');
            rowVersion = Convert.FromBase64String(valor);
            return true;
        }
        catch (FormatException)
        {
            rowVersion = [];
            return false;
        }
    }
}
