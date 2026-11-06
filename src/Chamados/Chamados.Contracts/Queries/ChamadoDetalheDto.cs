namespace Chamados.Contracts;

/// <summary>Detalhe de um chamado, como o cliente vê no corpo da resposta.</summary>
public sealed record ChamadoDetalheDto(
    Guid Id,
    Guid SolicitanteId,
    Guid CategoriaId,
    Guid EquipeId,
    PrioridadeChamado Prioridade,
    StatusChamado Status,
    DateTimeOffset AbertoEm,
    DateTimeOffset PrazoSla,
    Guid? TecnicoAtribuidoId,
    string? NotaResolucao,
    DateTimeOffset? ResolvidoEm,
    DateTimeOffset? FechadoEm,
    bool Escalonado,
    DateTimeOffset? DataEscalonamento);

/// <summary>
/// Detalhe mais a versão de concorrência (RowVersion em base64), lidos na mesma consulta. A
/// versão nunca vai no corpo JSON: a Api a expõe só como header ETag (arquitetura/06).
/// </summary>
public sealed record ChamadoDetalheVersionado(ChamadoDetalheDto Chamado, string Versao);
