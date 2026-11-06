using Mediator;
using SharedKernel;

namespace Catalogo.Contracts;

/// <summary>
/// Query pública de Catalogo, consultada pela implementação de IChamadosAuthorizationContext
/// (Chamados.Infrastructure.ChamadoAuthorizationContext), que despacha esta Query via ISender em vez de referenciar o
/// interior de Catalogo (arquitetura/04-comunicacao-entre-modulos.md). Checa se o técnico
/// pertence à equipe informada — vínculo técnico-equipe é dado do módulo Catalogo.
/// </summary>
public sealed record EhMembroDaEquipeQuery(Guid TecnicoId, Guid EquipeId) : IRequest<Result<bool>>;
