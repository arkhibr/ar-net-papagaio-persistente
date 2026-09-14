using Chamados.Contracts;

namespace Api.Controllers.Chamados;

/// <summary>
/// Corpo de POST /api/v1/chamados. SolicitanteId NUNCA vem do corpo (comentário de
/// AbrirChamadoCommand: "SolicitanteId chega no Command resolvido do ICurrentUser pelo
/// controller, nunca confiado vindo do corpo da requisição, arquitetura/11") — por isso este
/// request não tem campo SolicitanteId; o controller resolve de ICurrentUser.UserId.
/// Campos na língua de domínio, camelCase (arquitetura/09-convencoes-rest-api.md).
/// </summary>
public sealed record AbrirChamadoRequest(Guid CategoriaId, PrioridadeChamado Prioridade);
