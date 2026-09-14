using SharedKernel;

namespace Api.Infrastructure;

/// <summary>
/// Implementação real de ICurrentUser (SharedKernel) lendo de HttpContext.User (claims).
/// Registrada aqui, na Api (composição raiz), e não em SharedKernel nem em módulo algum,
/// porque só a composição raiz conhece HttpContext (SharedKernel.ICurrentUser é interface
/// pura, arquitetura/11-autenticacao-e-sessao.md: "Domain nunca depende disto; só
/// Application/Api").
///
/// PENDÊNCIA EXPLÍCITA (não é lacuna silenciosa, é escopo desta rodada): não existe ainda
/// nenhum endpoint de login/emissão de cookie de sessão (arquitetura/11-autenticacao-e-sessao.md
/// completo — identity provider, hash de senha, emissão de cookie). O que existe aqui é só o
/// scaffolding mínimo (AddAuthentication().AddCookie(...) em Program.cs) para (a) os atributos
/// [Authorize]/[Authorize(Roles=...)] dos controllers funcionarem sem exceção de configuração
/// ausente, e (b) esta classe ter algo determinístico para ler de HttpContext.User quando uma
/// automação de teste (Api.IntegrationTests, próxima etapa do pipeline) construir um
/// ClaimsPrincipal manualmente e autenticar a requisição por fora (ex.: TestAuthHandler). Emitir
/// esse cookie de verdade a partir de usuário/senha reais fica para uma rodada dedicada a
/// arquitetura/11 — não inventado por conta própria aqui.
///
/// Claims esperadas (convenção desta composição, documentada aqui por não haver ainda emissor
/// real de identidade que as defina em outro lugar):
///   - ClaimTypes.NameIdentifier: UserId (Guid)
///   - "sid" (SessionId, claim custom): SessionId (Guid) — 0 quando ausente (ex.: identidade de
///     sistema, que não tem sessão HTTP de origem, arquitetura/21-auditoria.md "Job gera sua
///     própria correlação por ciclo").
///   - ClaimTypes.Role: um ou mais papéis (Solicitante/Tecnico/Supervisor).
///   - "system_actor" (claim custom, valor "true"): identidade de ator automatizado (Worker),
///     nunca atribuível por login de usuário comum — só usada nesta solution pelo próprio
///     Worker (fora de escopo desta rodada) autenticando contra a Api por um mecanismo
///     máquina-a-máquina ainda não implementado.
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

    public bool IsSystemActor => Principal?.FindFirst("system_actor")?.Value == "true";

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
