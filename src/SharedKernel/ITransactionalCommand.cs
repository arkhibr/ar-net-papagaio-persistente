namespace SharedKernel;

/// <summary>
/// Marca um Command cujo handler muda estado de agregado e precisa do commit único do
/// UnitOfWorkBehavior (arquitetura/25-transacao-e-unit-of-work.md). Query nunca implementa
/// este marcador: leitura não tem transação de escrita nem commit (arquitetura/27-leitura-e-query-side.md).
///
/// Decisão registrada (api-e-composicao, lacuna deste SharedKernel): não existia marcador de
/// mutação antes desta rodada. Reaproveitar IIdempotentCommand como o marcador de mutação foi
/// descartado porque as duas coisas são ortogonais — idempotência é sobre reenvio da mesma
/// requisição (nem todo Command que muta precisa disso; nem todo Command idempotente
/// necessariamente teria que mutar, em tese) — usar o mesmo marcador para as duas
/// responsabilidades acoplaria dois conceitos que arquitetura/06 já trata como complementares,
/// não substitutos. Os 8 Commands de Chamados hoje mutam estado E são idempotentes ao mesmo
/// tempo (coincidência do domínio, não relação estrutural), então cada um implementa os dois
/// marcadores separadamente.
/// </summary>
public interface ITransactionalCommand
{
}
