using Chamados.Application;
using Chamados.Domain;

namespace Chamados.Application.UnitTests.Fakes;

/// <summary>
/// Fake in-memory de IChamadoRepository. Não distingue de fato tracking/no-tracking (isso é
/// conceito de EF Core, testado em Infrastructure.IntegrationTests) — aqui expõe os dois
/// métodos de leitura com a mesma assinatura da porta real, para o handler poder ser testado
/// sem saber que a implementação real seria diferente por dentro.
/// </summary>
internal sealed class FakeChamadoRepository : IChamadoRepository
{
    private readonly Dictionary<Guid, Chamado> _chamados = new();
    private readonly Dictionary<Guid, byte[]> _rowVersions = new();

    public IReadOnlyCollection<Chamado> Todos => _chamados.Values;

    /// <summary>Último rowVersionEsperado recebido por ObterParaEscritaAsync, por chamado — para
    /// testes confirmarem que o handler repassou o valor do Command à porta.</summary>
    public Dictionary<Guid, byte[]?> RowVersionEsperadoRecebido { get; } = new();

    public FakeChamadoRepository ComChamado(Chamado chamado)
    {
        _chamados[chamado.Id] = chamado;
        _rowVersions.TryAdd(chamado.Id, [1, 2, 3]);
        return this;
    }

    public FakeChamadoRepository ComRowVersion(Guid chamadoId, byte[] rowVersion)
    {
        _rowVersions[chamadoId] = rowVersion;
        return this;
    }

    public Task AdicionarAsync(Chamado chamado, CancellationToken cancellationToken)
    {
        _chamados[chamado.Id] = chamado;
        _rowVersions.TryAdd(chamado.Id, [1, 2, 3]);
        return Task.CompletedTask;
    }

    public Task<Chamado?> ObterParaEscritaAsync(
        Guid chamadoId, byte[]? rowVersionEsperado, CancellationToken cancellationToken)
    {
        RowVersionEsperadoRecebido[chamadoId] = rowVersionEsperado;
        return Task.FromResult(_chamados.GetValueOrDefault(chamadoId));
    }

    public Task<Chamado?> ObterSomenteLeituraAsync(Guid chamadoId, CancellationToken cancellationToken) =>
        Task.FromResult(_chamados.GetValueOrDefault(chamadoId));

    public Task<byte[]?> ObterRowVersionAsync(Guid chamadoId, CancellationToken cancellationToken) =>
        Task.FromResult(_rowVersions.GetValueOrDefault(chamadoId));

    public Task<IReadOnlyList<Chamado>> ListarPorSolicitanteAsync(
        Guid solicitanteId, int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Chamado>>(
            _chamados.Values.Where(c => c.SolicitanteId == solicitanteId).ToList());

    public Task<IReadOnlyList<Chamado>> ListarPorEquipeAsync(
        Guid equipeId, int page, int pageSize, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Chamado>>(
            _chamados.Values.Where(c => c.EquipeId == equipeId).ToList());

    public Task SalvarAsync(Chamado chamado, CancellationToken cancellationToken)
    {
        _chamados[chamado.Id] = chamado;
        return Task.CompletedTask;
    }
}
