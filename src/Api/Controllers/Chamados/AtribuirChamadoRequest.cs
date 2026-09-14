namespace Api.Controllers.Chamados;

/// <summary>
/// Corpo de POST /api/v1/chamados/{id}/atribuir. ChamadoId vem da rota (não duplicado no
/// corpo — ver nota em ChamadosController sobre "id da rota vira o campo do Command"),
/// RowVersion vem do header If-Match (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md,
/// "ETag/If-Match é a superfície HTTP do mesmo RowVersion... o cliente nunca lê nem escreve o
/// valor de versão no corpo"), nunca campo solto no JSON.
/// </summary>
public sealed record AtribuirChamadoRequest(Guid TecnicoId);
