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
/// fazer, e sem o cliente poder mandar um ChamadoId divergente por engano). Identidade nunca
/// vem do corpo: SolicitanteId (Abrir) e TecnicoId (Atribuir) saem de ICurrentUser
/// (arquitetura/11); RowVersion em Atribuir sai do header If-Match (arquitetura/06). O vínculo
/// do usuário com o chamado é conferido no pipeline (IsAuthorizedAsync), nunca aqui.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/chamados")]
public sealed class ChamadosController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public ChamadosController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
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
            _currentUser.UserId, request.CategoriaId, request.Prioridade, idempotencyKey);

        var resultado = await _sender.Send(command, cancellationToken);

        // CreatedAtAction(nameof(ObterAsync), ...) foi tentado e descartado: o link generator do
        // Asp.Versioning falha em resolver a rota versionada ("No route matches the supplied
        // values") mesmo passando o valor de versão explicitamente, tipado ou não — problema
        // conhecido, sem correção estável na versão do pacote usada aqui. Construindo o Location
        // a partir do próprio path da requisição atual (POST .../chamados -> .../chamados/{id}),
        // que preserva o segmento de versão real usado pelo cliente sem depender do link
        // generator.
        return resultado.ToActionResult(id => Created($"{Request.Path}/{id}", new { id }));
    }

    /// <summary>
    /// GET /api/v1/chamados/{id} — ObterChamadoQuery. Autorização no pipeline; "não existe" e
    /// "sem acesso" respondem o mesmo 404 (M3 de achados.md).
    ///
    /// ETag (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md): a versão lida
    /// na mesma consulta do corpo vai só no header ETag (opaco, base64); o cliente a devolve via
    /// If-Match sem nunca interpretar o valor.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        var resultado = await _sender.Send(new ObterChamadoQuery(id), cancellationToken);

        return resultado.ToActionResult(detalhe =>
        {
            // A versão só existe no header ETag, nunca no corpo (arquitetura/06; M6 de achados.md).
            Response.Headers.ETag = $"\"{detalhe.Versao}\"";
            return Ok(detalhe.Chamado);
        });
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
            pagina => Ok(PagedResponse<ChamadoResumoDto>.De(pagina, paginaValida, tamanhoValido)));
    }

    /// <summary>
    /// POST /api/v1/chamados/{id}/atribuir — AtribuirChamadoCommand (autoatribuição: o técnico é
    /// sempre o usuário autenticado, sem corpo; A1 de achados.md). Idempotency-Key +
    /// If-Match/RowVersion (plano-de-arquitetura.md secao 6). ETag desatualizado → 412.
    /// </summary>
    [HttpPost("{id:guid}/atribuir")]
    [Authorize]
    public async Task<IActionResult> AtribuirAsync(
        Guid id,
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
            return new ProblemResult(HttpErrors.MissingHeader(
                "If-Match",
                "A atribuição de um chamado exige o cabeçalho If-Match com o ETag obtido em GET /api/v1/chamados/{id}."));
        }

        var command = new AtribuirChamadoCommand(id, _currentUser.UserId, rowVersion, idempotencyKey);

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
    /// POST /api/v1/chamados/{id}/fechar — FecharChamadoCommand. Quem pode fechar (solicitante do
    /// chamado ou ator de sistema) é conferido no pipeline contra o estado persistido
    /// (A3 de achados.md); o controller não lê o chamado antes.
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

        var command = new FecharChamadoCommand(id, idempotencyKey);

        var resultado = await _sender.Send(command, cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>
    /// POST /api/v1/chamados/{id}/reabrir — ReabrirChamadoCommand. Mesma regra de FecharAsync, só
    /// para o solicitante.
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

        var command = new ReabrirChamadoCommand(id, idempotencyKey);

        var resultado = await _sender.Send(command, cancellationToken);

        return resultado.ToActionResult();
    }

    private static ProblemResult IdempotencyKeyAusente() => new(HttpErrors.MissingHeader(
        "Idempotency-Key",
        "Esta operação exige o cabeçalho Idempotency-Key (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md)."));

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
