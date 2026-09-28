using Chamados.Domain;

namespace Chamados.Application;

/// <summary>
/// Repositório de escrita do agregado Chamado. Leitura para exibição fica em IChamadoLeitura
/// (arquitetura/27-leitura-e-query-side.md, "Repositório de leitura vs. de escrita").
/// </summary>
internal interface IChamadoRepository
{
    Task AdicionarAsync(Chamado chamado, CancellationToken cancellationToken);

    /// <summary>
    /// Carrega rastreado para um método de ação. Com rowVersionEsperado (If-Match), lança
    /// PreconditionFailedException se a versão atual já é outra (412, arquitetura/06), antes de
    /// qualquer regra de domínio; a mesma versão continua protegendo o UPDATE até o commit.
    /// </summary>
    Task<Chamado?> ObterParaEscritaAsync(Guid chamadoId, byte[]? rowVersionEsperado, CancellationToken cancellationToken);

    Task SalvarAsync(Chamado chamado, CancellationToken cancellationToken);
}
