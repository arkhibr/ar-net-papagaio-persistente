using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>Chamados da equipe do técnico/supervisor autenticado, paginados (arquitetura/19).</summary>
public sealed record FilaDaEquipeQuery(int Page, int PageSize) : IRequest<Result<PagedResult<ChamadoResumoDto>>>;
