using Mediator;
using SharedKernel;

namespace Catalogo.Contracts;

/// <summary>
/// Query pública de Catalogo, consultada por Chamados a partir do guard explícito de
/// ReclassificarChamadoCommandHandler (plano-de-arquitetura.md secao 2: "técnico atribuído,
/// ou qualquer técnico da equipe se ainda Aberto" — decidido como guard fora do
/// IRequiresAuthorization padrão) e da implementação real de IEquipeMembershipChecker
/// (Chamados.Application), que despacha esta Query via ISender em vez de referenciar o
/// interior de Catalogo (arquitetura/04-comunicacao-entre-modulos.md). Checa se o técnico
/// pertence à equipe informada — vínculo técnico-equipe é dado do módulo Catalogo.
/// </summary>
public sealed record EhMembroDaEquipeQuery(Guid TecnicoId, Guid EquipeId) : IRequest<Result<bool>>;
