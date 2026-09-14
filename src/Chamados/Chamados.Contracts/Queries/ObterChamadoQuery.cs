using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Detalhe de um único chamado por id (rota GET /api/v1/chamados/{id}, plano-de-arquitetura.md
/// secao 6 — lacuna nunca implementada até esta rodada). Acesso direto por id: leitura B de
/// autorização por recurso (arquitetura/12-autorizacao-por-recurso.md) — mas resolvida como
/// guard explícito dentro do handler, NÃO como IRequiresAuthorization padrão, pelo mesmo
/// motivo documentado em ReclassificarChamadoCommand: a regra aqui ("o solicitante dono, o
/// técnico atualmente atribuído, ou qualquer técnico/supervisor da equipe responsável, em
/// qualquer status do chamado, pode ver o detalhe") precisa do SolicitanteId do próprio
/// agregado para o primeiro braço, e este Command não carrega SolicitanteId (diferente de
/// FecharChamadoCommand/ReabrirChamadoCommand, onde o chamador já sabe o dono de antemão —
/// aqui é justamente o que a Query existe para revelar). Reaproveitar
/// IAuthorizationContext.HasResourceLinkAsync (implementado por ChamadoAuthorizationContext)
/// também não serve: aquele método já tem semântica própria e mais estreita, específica de
/// Atribuir/Devolver/Resolver (só cobre "técnico atribuído" ou "membro de equipe enquanto
/// Aberto"), não "solicitante dono" nem "membro de equipe em qualquer status". Como o handler
/// já precisa carregar o Chamado para montar o DTO de resposta, o guard roda sobre o mesmo
/// carregamento, sem consulta duplicada — mesma decisão de ReclassificarChamadoCommandHandler.
///
/// Essa regra de "quem pode ver o detalhe" não está 100% explícita no plano/especificação;
/// é inferida por analogia direta ao que já vale para FilaDaEquipeQuery (papel
/// Técnico/Supervisor + escopo de equipe) e para o vínculo solicitante-chamado usado em
/// FecharChamadoCommand/ReabrirChamadoCommand — registrada aqui como a leitura mais coerente
/// com o resto do módulo, não uma regra nova inventada.
/// Tipo público em Contracts: tem rota HTTP (arquitetura/01).
/// </summary>
public sealed record ObterChamadoQuery(Guid ChamadoId) : IRequest<Result<ChamadoDetalheDto>>;
