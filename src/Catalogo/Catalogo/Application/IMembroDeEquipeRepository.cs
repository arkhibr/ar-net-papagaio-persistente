namespace Catalogo.Application;

/// <summary>
/// Porta de repositório do vínculo técnico-equipe. Decisão de modelagem (cqrs-e-casos-de-uso):
/// este vínculo é dado plano de referência (quem pertence a qual equipe), sem invariante de
/// negócio própria além da associação em si — não justifica um agregado/entidade rica em
/// Catalogo.Domain (que permanece majoritariamente anêmico, plano-de-arquitetura.md secao 2).
/// Consultado pelos handlers de EhMembroDaEquipeQuery/ResolverEquipeDoTecnicoQuery e alterado
/// pelos Commands de vínculo (Vincular/DesvincularMembroDaEquipeCommand).
/// Implementação real (EF Core) mora na Infrastructure, fora do escopo de
/// Application.UnitTests (persistencia-e-integracao).
/// </summary>
internal interface IMembroDeEquipeRepository
{
    Task<bool> EhMembroAsync(Guid tecnicoId, Guid equipeId, CancellationToken cancellationToken);

    /// <summary>
    /// Equipe única à qual o técnico pertence, ou null se não houver vínculo cadastrado.
    /// </summary>
    Task<Guid?> ResolverEquipeIdAsync(Guid tecnicoId, CancellationToken cancellationToken);

    /// <summary>Encena o vínculo; o commit é do UnitOfWorkBehavior.</summary>
    Task VincularAsync(Guid usuarioId, Guid equipeId, CancellationToken cancellationToken);

    /// <summary>Encena a remoção do vínculo. False se o usuário não for membro desta equipe.</summary>
    Task<bool> DesvincularAsync(Guid usuarioId, Guid equipeId, CancellationToken cancellationToken);
}
