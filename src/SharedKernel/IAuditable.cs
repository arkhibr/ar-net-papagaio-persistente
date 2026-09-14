namespace SharedKernel;

/// <summary>
/// Marca um Command cuja execução deve ser registrada para auditoria (arquitetura/21-auditoria.md),
/// gravada na mesma transação via interceptor. Opt-in por Command: só o subconjunto decidido em
/// especificacao-clarificada.md/plano-de-arquitetura.md (por exemplo, em Chamados: abertura,
/// atribuição, resolução, escalonamento) implementa este marcador — reclassificar/devolver/fechar/
/// reabrir não são auditados.
/// </summary>
public interface IAuditable;
