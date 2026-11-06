namespace Catalogo.Contracts;

/// <summary>
/// Prioridade tal como o módulo Catalogo a conhece, usada para resolver o SLA de uma
/// categoria/prioridade (ResolverEquipeESlaQuery). Não é o mesmo tipo que
/// Chamados.Domain.PrioridadeChamado: cada módulo tem seu próprio vocabulário na
/// fronteira (arquitetura/04-comunicacao-entre-modulos.md, arquitetura/29). O handler de
/// Chamados que despacha esta Query faz a tradução entre os dois enums.
/// </summary>
public enum PrioridadeServico
{
    Baixa = 0,
    Media = 1,
    Alta = 2,
    Critica = 3,
}
