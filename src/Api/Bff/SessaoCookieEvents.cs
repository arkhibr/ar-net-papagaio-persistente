using System.Security.Claims;
using Api.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Api.Bff;

/// <summary>
/// Eventos do cookie de sessão (adaptacao-bff-angular.md, B2/B7). A cada requisição, o cookie só
/// vale se a AuthSession correspondente for válida no servidor (revogação, expiração e
/// SecurityVersion, arquitetura/11). Sem sessão, /api/* responde 401 em problem+json, nunca
/// redirect.
/// </summary>
internal sealed class SessaoCookieEvents : CookieAuthenticationEvents
{
    internal const string ChaveSessaoValida = "Bff.SessaoValida";

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var sessoes = context.HttpContext.RequestServices.GetRequiredService<SessoesDeAutenticacao>();

        var valida = Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                     && Guid.TryParse(principal?.FindFirstValue(SessoesDeAutenticacao.ClaimSessao), out var sessionId)
            ? await sessoes.ValidarAsync(sessionId, userId, context.HttpContext.RequestAborted)
            : null;

        if (valida is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        // Lido por PapeisDoServidorTransformation na mesma requisição, sem segunda consulta.
        context.HttpContext.Items[ChaveSessaoValida] = valida;
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context) =>
        ProblemResult.WriteAsync(context.HttpContext, HttpErrors.Unauthorized());

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context) =>
        ProblemResult.WriteAsync(context.HttpContext, HttpErrors.Forbidden("Você não tem o papel exigido para esta operação."));
}

/// <summary>
/// Acrescenta ao principal da requisição os papéis lidos do servidor (decisão D2; B8). As claims
/// de papel nunca vão para o cookie: a transformação roda depois do handler do cookie, sobre uma
/// cópia do principal. ICurrentUser.IsInRole e [Authorize(Roles = ...)] continuam iguais.
/// </summary>
internal sealed class PapeisDoServidorTransformation : IClaimsTransformation
{
    private const string TipoDaIdentidade = "papeis-servidor";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public PapeisDoServidorTransformation(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (_httpContextAccessor.HttpContext?.Items[SessaoCookieEvents.ChaveSessaoValida] is not SessaoValida sessao
            || principal.Identities.Any(i => i.AuthenticationType == TipoDaIdentidade))
        {
            return Task.FromResult(principal);
        }

        var transformado = principal.Clone();
        transformado.AddIdentity(new ClaimsIdentity(
            sessao.Papeis.Select(papel => new Claim(ClaimTypes.Role, papel)),
            TipoDaIdentidade));
        return Task.FromResult(transformado);
    }
}
