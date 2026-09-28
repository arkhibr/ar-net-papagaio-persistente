using SharedKernel;

namespace Api.Infrastructure;

/// <summary>
/// Implementação real de ICurrentUser (SharedKernel) lendo de HttpContext.User (claims).
/// Registrada aqui, na Api (composição raiz), e não em SharedKernel nem em módulo algum,
/// porque só a composição raiz conhece HttpContext (arquitetura/11: "Domain nunca depende
/// disto; só Application/Api").
///
/// Claims (adaptacao-bff-angular.md, B7/B8):
///   - ClaimTypes.NameIdentifier: UserId (Guid), gravado no cookie.
///   - "sid": SessionId da AuthSession, gravado no cookie e validado a cada requisição
///     (SessaoCookieEvents).
///   - ClaimTypes.Role: acrescentadas por requisição a partir de UsuarioPapel
///     (PapeisDoServidorTransformation), nunca gravadas no cookie.
///
/// IsSystemActor é sempre false numa requisição HTTP: o ator de sistema é do Worker
/// (arquitetura/26, ICurrentUserAccessor.BeginSystemIdentity), nunca de um login HTTP (B8).
/// </summary>
internal sealed class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private System.Security.Claims.ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public Guid UserId
    {
        get
        {
            var valor = Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(valor, out var userId) ? userId : Guid.Empty;
        }
    }

    public Guid SessionId
    {
        get
        {
            var valor = Principal?.FindFirst("sid")?.Value;
            return Guid.TryParse(valor, out var sessionId) ? sessionId : Guid.Empty;
        }
    }

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsSystemActor => false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
