using Mediator;
using SharedKernel;

namespace Catalogo.Contracts;

/// <summary>
/// Query pública de Catalogo, consultada por Chamados (AbrirChamadoCommand e
/// ReclassificarChamadoCommand) para resolver equipe responsável + horas de SLA a partir
/// da categoria + prioridade, no momento em que o snapshot precisa ser congelado
/// (plano-de-arquitetura.md secao 2 "Snapshot vs. referência viva"; especificacao-clarificada.md).
/// Nunca o inverso: Catalogo nunca referencia Chamados.
/// </summary>
public sealed record ResolverEquipeESlaQuery(Guid CategoriaId, PrioridadeServico Prioridade)
    : IRequest<Result<ResolverEquipeESlaResultado>>;

public sealed record ResolverEquipeESlaResultado(Guid EquipeId, int HorasDeSla);
