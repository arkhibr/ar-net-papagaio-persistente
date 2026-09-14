namespace Chamados.Contracts;

/// <summary>
/// DTO de leitura para listagens (MeusChamadosQuery, FilaDaEquipeQuery). Nunca o agregado
/// exposto direto. Fica em Contracts: acompanha as Queries que o expõem, que a Api precisa
/// nomear para desserializar/retornar (arquitetura/01).
/// </summary>
public sealed record ChamadoResumoDto(
    Guid Id,
    Guid CategoriaId,
    Guid EquipeId,
    PrioridadeChamado Prioridade,
    StatusChamado Status,
    DateTimeOffset AbertoEm,
    DateTimeOffset PrazoSla,
    Guid? TecnicoAtribuidoId,
    bool Escalonado);
