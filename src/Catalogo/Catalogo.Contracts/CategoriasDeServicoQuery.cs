using SharedKernel;
using Mediator;

namespace Catalogo.Contracts;

/// <summary>
/// Lista todas as categorias de serviço. Autorização: qualquer autenticado (leitura A,
/// [Authorize] genérico na Api — não implementa IRequiresAuthorization). Cacheável
/// (plano-de-arquitetura.md secao 5). Tipo público em Contracts: tem rota HTTP
/// (arquitetura/01-estrutura-de-projetos-monolito-modular.md).
/// </summary>
public sealed record CategoriasDeServicoQuery : IRequest<Result<IReadOnlyList<CategoriaDeServicoDto>>>, ICacheableQuery
{
    public string CacheKey => "categorias-de-servico";
}

public sealed record CategoriaDeServicoDto(Guid Id, string Nome, Guid EquipeId);
