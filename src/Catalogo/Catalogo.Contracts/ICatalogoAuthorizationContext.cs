using SharedKernel;

namespace Catalogo.Contracts;

/// <summary>
/// Contexto de autorização do Catálogo (arquitetura/12). Hoje nenhuma regra do módulo depende de
/// vínculo ator-recurso, então não há pergunta nomeada: a interface existe para o
/// AuthorizationBehavior achar o contexto do módulo dono da mensagem. A primeira regra por recurso
/// entra aqui como método.
/// </summary>
public interface ICatalogoAuthorizationContext : IAuthorizationContext;

/// <summary>Papéis que o Catálogo conhece.</summary>
public static class PapeisDoCatalogo
{
    /// <summary>Quem administra categorias, SLAs, equipes e membros.</summary>
    public const string Administrador = "Supervisor";
}

/// <summary>
/// Marca um Command ou Query da administração do Catálogo: só o papel administrador passa, para
/// qualquer chamador de Contracts (não só pela Api, que também confere com [Authorize(Roles)]).
/// Mesmo princípio de A1 de achados.md: a regra fica na mensagem, não só na borda HTTP.
/// </summary>
public interface IRequerAdministradorDoCatalogo : IRequiresAuthorization<ICatalogoAuthorizationContext>
{
    Task<bool> IRequiresAuthorization<ICatalogoAuthorizationContext>.IsAuthorizedAsync(
        ICurrentUser currentUser, ICatalogoAuthorizationContext context, CancellationToken cancellationToken) =>
        Task.FromResult(currentUser.IsAuthenticated && currentUser.IsInRole(PapeisDoCatalogo.Administrador));
}
