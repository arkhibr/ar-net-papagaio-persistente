using Chamados.Contracts;
using SharedKernel;

namespace Chamados.Application;

/// <summary>
/// Leitura para exibição: projeção direta para DTO no banco, sem carregar o agregado e sem
/// rastreamento (arquitetura/27-leitura-e-query-side.md, "Projeção direta para DTO"). Quem chama
/// já passou pela autorização do pipeline (ObterChamadoQuery) ou filtra pelo ator na assinatura
/// (listagens, arquitetura/14).
/// </summary>
internal interface IChamadoLeitura
{
    /// <summary>Detalhe e versão (RowVersion) lidos na mesma consulta.</summary>
    Task<ChamadoDetalheVersionado?> ObterDetalheAsync(Guid chamadoId, CancellationToken cancellationToken);

    Task<PagedResult<ChamadoResumoDto>> ListarPorSolicitanteAsync(
        Guid solicitanteId, int page, int pageSize, CancellationToken cancellationToken);

    Task<PagedResult<ChamadoResumoDto>> ListarPorEquipeAsync(
        Guid equipeId, int page, int pageSize, CancellationToken cancellationToken);
}
