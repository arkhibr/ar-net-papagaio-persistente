using Chamados.Domain;

namespace Chamados.Application;

/// <summary>
/// Porta de repositório do agregado Chamado. Implementação real (EF Core) mora na
/// Infrastructure, fora do escopo de Application.UnitTests (persistencia-e-integracao).
///
/// Duas leituras por id, com propósitos diferentes (13-estrategia-de-testes.md, "leitura
/// rastreada vs. não rastreada"): ObterParaEscritaAsync rastreia a entidade (tracking, usado
/// quando o handler vai chamar um método de ação e depois SalvarAsync); ObterSomenteLeituraAsync
/// não rastreia (no-tracking, usado em Query pura, ex. detalhe de um chamado).
/// </summary>
internal interface IChamadoRepository
{
    Task AdicionarAsync(Chamado chamado, CancellationToken cancellationToken);

    /// <summary>
    /// rowVersionEsperado: quando não-nulo (ETag/If-Match decodificado pelo controller a partir
    /// de um GET anterior), sobrescreve o OriginalValue do RowVersion capturado do banco — é
    /// isso que faz o UPDATE subsequente (WHERE RowVersion=@rowVersionEsperado) rejeitar a
    /// gravação se o cliente estava com um ETag desatualizado, fechando o ciclo real de
    /// If-Match/RowVersion (arquitetura/06). Handlers sem noção de ETag (todos exceto
    /// AtribuirChamadoCommand) passam null: a checagem de concorrência cai de volta para o
    /// valor lido do banco no load, que ainda detecta duas requisições diferentes disputando o
    /// mesmo agregado (proteção nunca ausente, só sem o reforço do valor enviado pelo cliente).
    /// </summary>
    Task<Chamado?> ObterParaEscritaAsync(Guid chamadoId, byte[]? rowVersionEsperado, CancellationToken cancellationToken);

    Task<Chamado?> ObterSomenteLeituraAsync(Guid chamadoId, CancellationToken cancellationToken);

    /// <summary>
    /// RowVersion atual (shadow property EF Core) do chamado, para a Api montar o header ETag
    /// em GET /chamados/{id} (ChamadoDetalheDto.RowVersion, arquitetura/06). Null se o chamado
    /// não existe.
    /// </summary>
    Task<byte[]?> ObterRowVersionAsync(Guid chamadoId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Chamado>> ListarPorSolicitanteAsync(
        Guid solicitanteId, int page, int pageSize, CancellationToken cancellationToken);

    Task<IReadOnlyList<Chamado>> ListarPorEquipeAsync(
        Guid equipeId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Encena a mudança no change tracker; o commit é do UnitOfWorkBehavior.</summary>
    Task SalvarAsync(Chamado chamado, CancellationToken cancellationToken);
}
