namespace Chamados.Contracts;

/// <summary>
/// Prioridade do chamado, escolhida pelo solicitante na abertura (especificacao-clarificada.md,
/// "Quem define prioridade"), podendo ser reclassificada depois via ReclassificarChamadoCommand.
/// Fica em Contracts (não em Domain) porque aparece na assinatura pública de
/// AbrirChamadoCommand/ReclassificarChamadoCommand/ChamadoResumoDto; Domain a referencia daqui
/// (Chamados já referencia Chamados.Contracts). Cada prioridade tem um SLA de resolução fixo,
/// em horas corridas (especificacao-clarificada.md): Critica 4h, Alta 8h, Media 24h, Baixa 72h.
/// </summary>
public enum PrioridadeChamado
{
    Baixa = 0,
    Media = 1,
    Alta = 2,
    Critica = 3,
}
