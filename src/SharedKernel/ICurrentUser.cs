namespace SharedKernel;

/// <summary>
/// Definição canônica de ICurrentUser (arquitetura/11-autenticacao-e-sessao.md).
/// Identidade pura de quem pede; nunca consulta vínculo ator-recurso (isso é
/// IAuthorizationContext). Domain nunca depende disto; só Application/Api.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
    Guid SessionId { get; }
    bool IsAuthenticated { get; }

    /// <summary>True para identidade de ator automatizado (job/worker), ver 04 e 21.</summary>
    bool IsSystemActor { get; }

    /// <summary>Papel genérico, para a leitura A de autorização (arquitetura/12-autorizacao-por-recurso.md).</summary>
    bool IsInRole(string role);
}
