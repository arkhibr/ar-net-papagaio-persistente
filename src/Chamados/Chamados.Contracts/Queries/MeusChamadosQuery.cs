using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>Chamados abertos pelo usuário autenticado, paginados (arquitetura/19).</summary>
public sealed record MeusChamadosQuery(int Page, int PageSize) : IRequest<Result<PagedResult<ChamadoResumoDto>>>;
