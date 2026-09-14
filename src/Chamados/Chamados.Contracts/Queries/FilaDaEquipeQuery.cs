using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Lista os chamados da fila de uma equipe, paginada, não cacheável (plano-de-arquitetura.md
/// secao 5/6). Autorização: papel Técnico/Supervisor + filtro por linha (equipe do próprio
/// ator, resolvida na Infrastructure a partir de ICurrentUser, nunca aceita como EquipeId
/// vindo do cliente) — mesma lógica de "Listagem" de MeusChamadosQuery, sem
/// IRequiresAuthorization.
/// Tipo público em Contracts: tem rota HTTP (arquitetura/01).
/// </summary>
public sealed record FilaDaEquipeQuery(int Page, int PageSize) : IRequest<Result<IReadOnlyList<ChamadoResumoDto>>>;
