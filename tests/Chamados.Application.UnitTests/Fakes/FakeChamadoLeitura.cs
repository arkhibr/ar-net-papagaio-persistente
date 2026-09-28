using Chamados.Application;
using Chamados.Contracts;
using SharedKernel;

namespace Chamados.Application.UnitTests.Fakes;

/// <summary>
/// Fake de IChamadoLeitura (porta de leitura, arquitetura/27-leitura-e-query-side.md). A
/// projeção e a paginação reais são testadas em Infrastructure.IntegrationTests; aqui as
/// respostas são configuradas por teste, e os argumentos recebidos ficam registrados para
/// confirmar que o handler filtrou pelo ator certo (arquitetura/14).
/// </summary>
internal sealed class FakeChamadoLeitura : IChamadoLeitura
{
    private readonly Dictionary<Guid, ChamadoDetalheVersionado> _detalhes = new();
    private readonly Dictionary<Guid, PagedResult<ChamadoResumoDto>> _porSolicitante = new();
    private readonly Dictionary<Guid, PagedResult<ChamadoResumoDto>> _porEquipe = new();

    public List<(Guid SolicitanteId, int Page, int PageSize)> ConsultasPorSolicitante { get; } = [];
    public List<(Guid EquipeId, int Page, int PageSize)> ConsultasPorEquipe { get; } = [];

    public FakeChamadoLeitura ComDetalhe(ChamadoDetalheVersionado detalhe)
    {
        _detalhes[detalhe.Chamado.Id] = detalhe;
        return this;
    }

    public FakeChamadoLeitura ComPaginaDoSolicitante(Guid solicitanteId, PagedResult<ChamadoResumoDto> pagina)
    {
        _porSolicitante[solicitanteId] = pagina;
        return this;
    }

    public FakeChamadoLeitura ComPaginaDaEquipe(Guid equipeId, PagedResult<ChamadoResumoDto> pagina)
    {
        _porEquipe[equipeId] = pagina;
        return this;
    }

    public Task<ChamadoDetalheVersionado?> ObterDetalheAsync(Guid chamadoId, CancellationToken cancellationToken) =>
        Task.FromResult(_detalhes.GetValueOrDefault(chamadoId));

    public Task<PagedResult<ChamadoResumoDto>> ListarPorSolicitanteAsync(
        Guid solicitanteId, int page, int pageSize, CancellationToken cancellationToken)
    {
        ConsultasPorSolicitante.Add((solicitanteId, page, pageSize));
        return Task.FromResult(_porSolicitante.GetValueOrDefault(solicitanteId) ?? Vazia());
    }

    public Task<PagedResult<ChamadoResumoDto>> ListarPorEquipeAsync(
        Guid equipeId, int page, int pageSize, CancellationToken cancellationToken)
    {
        ConsultasPorEquipe.Add((equipeId, page, pageSize));
        return Task.FromResult(_porEquipe.GetValueOrDefault(equipeId) ?? Vazia());
    }

    private static PagedResult<ChamadoResumoDto> Vazia() => new([], 0);
}
