using Api.Bff;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.Controllers.Auth;

/// <summary>
/// GET /api/me — o que o Angular pode saber sobre a sessão, só para UX (arquitetura/17, "O que o
/// Angular pode saber sobre a sessão"; B9 de achados.md; adaptacao-bff-angular.md, B2). Nunca é
/// mecanismo de autorização. Rota técnica, fora do versionamento. Também reemite o XSRF-TOKEN
/// para o bootstrap do Angular sair com o token da identidade atual.
/// </summary>
[ApiController]
[ApiVersionNeutral]
[Route("api/me")]
public sealed class MeController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult Obter([FromServices] ICurrentUser currentUser)
    {
        XsrfCookie.Emitir(HttpContext);

        var sessao = HttpContext.Items[SessaoCookieEvents.ChaveSessaoValida] as SessaoValida;
        return Ok(new MeResponse(
            currentUser.UserId,
            sessao?.Nome,
            CadastroDeUsuarios.PapeisConhecidos.Where(currentUser.IsInRole).ToArray(),
            sessao?.Sessao.ExpiresAt));
    }

    /// <summary>Nome é null enquanto não for definido no cadastro. Nunca sid, token ou claim bruta.</summary>
    public sealed record MeResponse(Guid UserId, string? Nome, string[] Papeis, DateTimeOffset? SessaoExpiraEm);
}
