namespace Chamados.Contracts;

/// <summary>
/// DTO de leitura para o detalhe de um único chamado (ObterChamadoQuery, rota
/// GET /api/v1/chamados/{id}, plano-de-arquitetura.md secao 6). Não reaproveita
/// ChamadoResumoDto (pensado para listagem) porque o detalhe de um chamado específico
/// precisa de campos que a listagem não expõe: SolicitanteId (a própria checagem de
/// autorização do handler compara contra ele), NotaResolucao/ResolvidoEm/FechadoEm
/// (histórico da transição, só relevante olhando um chamado por vez) e DataEscalonamento
/// (par completo da flag Escalonado, e não só a flag).
///
/// RowVersion: opaco, base64 do shadow property EF Core (nunca exposto como número/formato
/// interpretável pelo cliente — arquitetura/06). A Api usa este valor para montar o header
/// ETag da resposta; o cliente só devolve o mesmo valor via If-Match em
/// POST /chamados/{id}/atribuir, nunca decodifica nem interpreta. Fecha o ciclo que antes
/// deixava AtribuirChamadoCommand exigir If-Match sem nenhum GET fornecer um valor legítimo
/// (achado de revisão de código + testes-manuais.md).
/// </summary>
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
    DateTimeOffset? DataEscalonamento,
    string RowVersion);
