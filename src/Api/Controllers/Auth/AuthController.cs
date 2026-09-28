using Api.Bff;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SharedKernel;

namespace Api.Controllers.Auth;

/// <summary>
/// Rotas de autenticação e sessão sob /auth/* (arquitetura/17, "Mesma origem";
/// adaptacao-bff-angular.md, B1/B4/B10). Fora do versionamento: é mecânica técnica da borda, não
/// contrato de negócio. É controller MVC para o filtro global de antiforgery cobrir os POSTs.
/// POST /auth/login é form post da view Views/Auth/Login.cshtml ([FromForm] explícito).
/// </summary>
[ApiController]
[ApiVersionNeutral]
[Route("auth")]
public sealed class AuthController : Controller
{
    private const string MensagemDeFalha = "Login ou senha inválidos.";

    /// <summary>GET /auth/csrf — (re)emite o cookie XSRF-TOKEN para a identidade atual (B1).</summary>
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult Csrf()
    {
        XsrfCookie.Emitir(HttpContext);
        return NoContent();
    }

    /// <summary>GET /auth/login?returnUrl= — página de login do backend (B10).</summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string? returnUrl)
    {
        var destino = DestinoLocal(returnUrl);
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(destino);
        }

        return Pagina(destino, erro: null);
    }

    /// <summary>
    /// POST /auth/login — form post. Antiforgery de formulário validado pelo filtro global; rate
    /// limiting por IP. Sucesso cria a AuthSession (B7) e redireciona; falha rerenderiza com a
    /// mesma mensagem genérica, sem dizer se o login existe (arquitetura/11, Nota de aplicação).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(BffDependencyInjection.PoliticaLoginLocal)]
    public async Task<IActionResult> LoginAsync(
        [FromForm] string? login,
        [FromForm] string? senha,
        [FromForm] string? returnUrl,
        CancellationToken cancellationToken)
    {
        var loginLocal = HttpContext.RequestServices.GetRequiredService<LoginLocal>();
        var sessoes = HttpContext.RequestServices.GetRequiredService<SessoesDeAutenticacao>();
        var destino = DestinoLocal(returnUrl);
        var userId = await loginLocal.AutenticarAsync(login, senha, cancellationToken);
        if (userId is null)
        {
            return Pagina(destino, MensagemDeFalha);
        }

        var sessao = await sessoes.CriarAsync(userId.Value, cancellationToken);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, SessoesDeAutenticacao.CriarPrincipal(sessao));
        return LocalRedirect(destino);
    }

    /// <summary>
    /// POST /auth/logout — revoga a AuthSession (B7), apaga o cookie e diz ao Angular para onde
    /// navegar (B4). Validado por CSRF pelo filtro global.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> LogoutAsync(
        [FromServices] ICurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        // Serviços internal da borda: resolvidos aqui porque a assinatura de action pública não
        // pode expô-los.
        var sessoes = HttpContext.RequestServices.GetRequiredService<SessoesDeAutenticacao>();
        if (currentUser.IsAuthenticated)
        {
            await sessoes.RevogarAsync(currentUser.SessionId, cancellationToken);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { redirectUrl = "/auth/login" });
    }

    /// <summary>returnUrl sempre local, para evitar open redirect (B4).</summary>
    private string DestinoLocal(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

    private ViewResult Pagina(string destino, string? erro)
    {
        var ambiente = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();

        Response.Headers.CacheControl = "no-store";
        return View("Login", new PaginaDeLoginModel(destino, erro, ambiente.IsDevelopment()));
    }
}
