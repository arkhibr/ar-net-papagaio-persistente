using Chamados.Contracts;

namespace Api.Controllers.Chamados;

/// <summary>Corpo de POST /api/v1/chamados/{id}/reclassificar. ChamadoId vem da rota.</summary>
public sealed record ReclassificarChamadoRequest(PrioridadeChamado NovaPrioridade);
