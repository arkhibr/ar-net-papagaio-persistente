using SharedKernel;

namespace Chamados.Contracts;

/// <summary>
/// Perguntas de vínculo ator-chamado, nomeadas por intenção (arquitetura/12-autorizacao-por-recurso.md),
/// usadas por IsAuthorizedAsync dos Commands/Queries deste módulo. Cada Command escolhe a
/// pergunta que corresponde à sua regra; a transição de status continua sendo invariante do
/// agregado. Implementada na Infrastructure do módulo.
/// </summary>
public interface IChamadosAuthorizationContext : IAuthorizationContext
{
    /// <summary>O usuário abriu o chamado.</summary>
    Task<bool> EhSolicitanteAsync(Guid userId, Guid chamadoId, CancellationToken cancellationToken);

    /// <summary>O usuário é o técnico atualmente atribuído ao chamado.</summary>
    Task<bool> EhTecnicoAtribuidoAsync(Guid userId, Guid chamadoId, CancellationToken cancellationToken);

    /// <summary>O usuário é membro da equipe responsável pelo chamado (dado do Catálogo).</summary>
    Task<bool> EhMembroDaEquipeResponsavelAsync(Guid userId, Guid chamadoId, CancellationToken cancellationToken);

    /// <summary>O chamado está Aberto, na fila da equipe de que o usuário é membro.</summary>
    Task<bool> EstaNaFilaDaEquipeDoUsuarioAsync(Guid userId, Guid chamadoId, CancellationToken cancellationToken);
}
