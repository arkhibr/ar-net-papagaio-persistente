using SharedKernel;
using Mediator;

namespace Catalogo.Contracts;

/// <summary>
/// Lista as categorias de serviço. Por padrão só as ativas (as que aceitam chamado novo), para
/// qualquer autenticado (leitura A, [Authorize] genérico na Api). IncluirInativas é para a
/// administração do Catálogo: IsAuthorizedAsync só aceita a flag para o papel administrador,
/// antes do cache (a ordem do pipeline põe Authorization antes de Caching). Cacheável; os Commands de
/// administração invalidam as duas entradas (ICacheInvalidator). Tipo público em Contracts: tem
/// rota HTTP (arquitetura/01-estrutura-de-projetos-monolito-modular.md).
/// </summary>
public sealed record CategoriasDeServicoQuery(bool IncluirInativas = false)
    : IRequest<Result<IReadOnlyList<CategoriaDeServicoDto>>>, IGlobalCacheableQuery,
      IRequiresAuthorization<ICatalogoAuthorizationContext>
{
    public Task<bool> IsAuthorizedAsync(
        ICurrentUser currentUser, ICatalogoAuthorizationContext context, CancellationToken cancellationToken) =>
        Task.FromResult(currentUser.IsAuthenticated && (!IncluirInativas || currentUser.IsInRole(PapeisDoCatalogo.Administrador)));

    public const string CacheKeyAtivas = "categorias-de-servico";
    public const string CacheKeyTodas = "categorias-de-servico:todas";

    public string CacheKey => IncluirInativas ? CacheKeyTodas : CacheKeyAtivas;
}

/// <summary>
/// Categoria com a equipe responsável e a tabela de SLA. EquipeNome, Ativa e Slas foram acrescentados
/// na v1 (campos novos não quebram o contrato). EquipeNome é null se a equipe não tiver cadastro.
/// </summary>
public sealed record CategoriaDeServicoDto(
    Guid Id, string Nome, Guid EquipeId, string? EquipeNome, bool Ativa, IReadOnlyList<SlaDto> Slas);

/// <summary>SLA de uma prioridade, em horas.</summary>
public sealed record SlaDto(PrioridadeServico Prioridade, int Horas);
