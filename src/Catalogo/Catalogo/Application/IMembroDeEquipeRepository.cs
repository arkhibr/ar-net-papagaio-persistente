namespace Catalogo.Application;

/// <summary>
/// Porta de repositório do vínculo técnico-equipe. Decisão de modelagem (cqrs-e-casos-de-uso):
/// este vínculo é dado plano de referência (quem pertence a qual equipe), sem invariante de
/// negócio própria além da associação em si — não justifica um agregado/entidade rica em
/// Catalogo.Domain (que permanece majoritariamente anêmico, plano-de-arquitetura.md secao 2).
/// Consultado só pelos handlers de EhMembroDaEquipeQuery/ResolverEquipeDoTecnicoQuery.
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
}
