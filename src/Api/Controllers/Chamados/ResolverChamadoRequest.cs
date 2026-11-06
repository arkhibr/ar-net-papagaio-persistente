namespace Api.Controllers.Chamados;

/// <summary>Corpo de POST /api/v1/chamados/{id}/resolver. ChamadoId vem da rota.</summary>
public sealed record ResolverChamadoRequest(string NotaResolucao);
