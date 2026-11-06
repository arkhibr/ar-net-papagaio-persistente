using Chamados.Application;
using Chamados.Domain;

namespace Chamados.Application.UnitTests.Fakes;

/// <summary>
/// Fake in-memory de IChamadoRepository (porta de escrita, arquitetura/27). Não reproduz
/// tracking nem a checagem de If-Match (PreconditionFailedException), que são testadas em
/// Infrastructure.IntegrationTests; só registra o rowVersionEsperado recebido para os testes
/// confirmarem que o handler o repassou à porta.
/// </summary>
internal sealed class FakeChamadoRepository : IChamadoRepository
{
    private readonly Dictionary<Guid, Chamado> _chamados = new();

    public IReadOnlyCollection<Chamado> Todos => _chamados.Values;

    /// <summary>Quantas vezes SalvarAsync foi chamado (0 quando o handler devolve Failure antes de salvar).</summary>
    public int Salvamentos { get; private set; }

    /// <summary>Último rowVersionEsperado recebido por ObterParaEscritaAsync, por chamado.</summary>
    public Dictionary<Guid, byte[]?> RowVersionEsperadoRecebido { get; } = new();

    public FakeChamadoRepository ComChamado(Chamado chamado)
    {
        _chamados[chamado.Id] = chamado;
        return this;
    }

    public Task AdicionarAsync(Chamado chamado, CancellationToken cancellationToken)
    {
        _chamados[chamado.Id] = chamado;
        return Task.CompletedTask;
    }

    public Task<Chamado?> ObterParaEscritaAsync(
        Guid chamadoId, byte[]? rowVersionEsperado, CancellationToken cancellationToken)
    {
        RowVersionEsperadoRecebido[chamadoId] = rowVersionEsperado;
        return Task.FromResult(_chamados.GetValueOrDefault(chamadoId));
    }

    public Task SalvarAsync(Chamado chamado, CancellationToken cancellationToken)
    {
        _chamados[chamado.Id] = chamado;
        Salvamentos++;
        return Task.CompletedTask;
    }
}
