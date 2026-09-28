using Catalogo.Contracts;
using Chamados.Contracts;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Chamados.Infrastructure;

/// <summary>
/// Implementação de IChamadosAuthorizationContext: perguntas de vínculo ator-chamado, uma por
/// intenção (M10 de achados.md). Lê só a projeção necessária do chamado (sem rastreamento,
/// arquitetura/27), uma vez por chamado por requisição, e consulta o vínculo técnico-equipe no
/// Catálogo via Catalogo.Contracts (arquitetura/04).
/// </summary>
internal sealed class ChamadoAuthorizationContext : IChamadosAuthorizationContext
{
    private readonly ChamadosDbContext _dbContext;
    private readonly ISender _sender;
    private readonly Dictionary<Guid, VinculosDoChamado?> _lidos = new();

    public ChamadoAuthorizationContext(ChamadosDbContext dbContext, ISender sender)
    {
        _dbContext = dbContext;
        _sender = sender;
    }

    public async Task<bool> EhSolicitanteAsync(Guid userId, Guid chamadoId, CancellationToken cancellationToken) =>
        (await LerAsync(chamadoId, cancellationToken))?.SolicitanteId == userId;

    public async Task<bool> EhTecnicoAtribuidoAsync(Guid userId, Guid chamadoId, CancellationToken cancellationToken) =>
        (await LerAsync(chamadoId, cancellationToken))?.TecnicoAtribuidoId == userId;

    public async Task<bool> EhMembroDaEquipeResponsavelAsync(Guid userId, Guid chamadoId, CancellationToken cancellationToken)
    {
        var chamado = await LerAsync(chamadoId, cancellationToken);

        return chamado is not null && await EhMembroAsync(userId, chamado.EquipeId, cancellationToken);
    }

    public async Task<bool> EstaNaFilaDaEquipeDoUsuarioAsync(Guid userId, Guid chamadoId, CancellationToken cancellationToken)
    {
        var chamado = await LerAsync(chamadoId, cancellationToken);

        return chamado is { Status: StatusChamado.Aberto }
            && await EhMembroAsync(userId, chamado.EquipeId, cancellationToken);
    }

    private async Task<bool> EhMembroAsync(Guid userId, Guid equipeId, CancellationToken cancellationToken)
    {
        var resultado = await _sender.Send(new EhMembroDaEquipeQuery(userId, equipeId), cancellationToken);

        return resultado.IsSuccess && resultado.Value;
    }

    private async Task<VinculosDoChamado?> LerAsync(Guid chamadoId, CancellationToken cancellationToken)
    {
        if (!_lidos.TryGetValue(chamadoId, out var vinculos))
        {
            vinculos = await _dbContext.Chamados
                .AsNoTracking()
                .Where(c => c.Id == chamadoId)
                .Select(c => new VinculosDoChamado(c.SolicitanteId, c.TecnicoAtribuidoId, c.EquipeId, c.Status))
                .FirstOrDefaultAsync(cancellationToken);

            _lidos[chamadoId] = vinculos;
        }

        return vinculos;
    }

    private sealed record VinculosDoChamado(Guid SolicitanteId, Guid? TecnicoAtribuidoId, Guid EquipeId, StatusChamado Status);
}
