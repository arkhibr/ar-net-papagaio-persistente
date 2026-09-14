using SharedKernel;
using Mediator;

namespace Chamados.Contracts;

/// <summary>
/// Reclassifica a prioridade do chamado, recalculando o prazo de SLA a partir da abertura
/// original (Chamado.Reclassificar). Autorização (lacuna 1 do plano-de-arquitetura.md, secao 2 e 5):
/// depende também do ESTADO do agregado (técnico atribuído, ou qualquer técnico da equipe
/// se ainda Aberto) — decidido como guard explícito dentro do handler, NÃO IRequiresAuthorization
/// padrão, porque o AuthorizationBehavior roda antes do agregado ser carregado e não tem
/// como enxergar o Status atual sem duplicar uma consulta que o handler já faz.
/// Por isso este Command não implementa IRequiresAuthorization.
/// Tipo público em Contracts: tem rota HTTP (arquitetura/01).
/// </summary>
public sealed record ReclassificarChamadoCommand(
    Guid ChamadoId,
    PrioridadeChamado NovaPrioridade,
    string IdempotencyKey)
    : IRequest<Result<Unit>>, IIdempotentCommand, ITransactionalCommand;
