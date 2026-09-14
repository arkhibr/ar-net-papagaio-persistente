using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Lista os chamados do próprio solicitante autenticado, paginada. Autorização: papel +
/// filtro por linha (arquitetura/12-autorizacao-por-recurso.md, "Listagem"): não implementa
/// IRequiresAuthorization porque não há um único recurso a autorizar contra — o que impede
/// vazamento é o repositório nunca oferecer consulta sem escopo por solicitante
/// (ListarPorSolicitanteAsync exige o SolicitanteId). SolicitanteId aqui é resolvido pelo
/// handler a partir de ICurrentUser.UserId, nunca aceito como parâmetro externo confiável.
/// Tipo público em Contracts: tem rota HTTP (arquitetura/01).
/// </summary>
public sealed record MeusChamadosQuery(int Page, int PageSize) : IRequest<Result<IReadOnlyList<ChamadoResumoDto>>>;
