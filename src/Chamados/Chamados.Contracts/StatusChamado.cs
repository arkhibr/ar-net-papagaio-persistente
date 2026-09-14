namespace Chamados.Contracts;

/// <summary>
/// Estado do ciclo de vida do Chamado. Escalonado NÃO é um estado: é uma marca
/// (flag + data) aplicável enquanto o chamado está Aberto ou EmAtendimento
/// (ver plano-de-arquitetura.md, secao 2). Fica em Contracts porque aparece em
/// ChamadoResumoDto, exposto pela Api.
/// </summary>
public enum StatusChamado
{
    Aberto = 0,
    EmAtendimento = 1,
    Resolvido = 2,
    Fechado = 3,
}
