using Chamados.Contracts;

namespace Chamados.Application.UnitTests.Fakes;

/// <summary>
/// Fake de IChamadosAuthorizationContext (arquitetura/12-autorizacao-por-recurso.md): cada
/// pergunta nomeada por intenção tem seu próprio conjunto de vínculos, configurado
/// explicitamente por teste. Nenhum vínculo configurado = resposta false.
/// </summary>
public sealed class FakeAuthorizationContext : IChamadosAuthorizationContext
{
    private readonly HashSet<(Guid UserId, Guid ChamadoId)> _solicitantes = new();
    private readonly HashSet<(Guid UserId, Guid ChamadoId)> _tecnicosAtribuidos = new();
    private readonly HashSet<(Guid UserId, Guid ChamadoId)> _membrosDaEquipe = new();
    private readonly HashSet<(Guid UserId, Guid ChamadoId)> _naFilaDaEquipe = new();

    public FakeAuthorizationContext ComSolicitante(Guid userId, Guid chamadoId)
    {
        _solicitantes.Add((userId, chamadoId));
        return this;
    }

    public FakeAuthorizationContext ComTecnicoAtribuido(Guid userId, Guid chamadoId)
    {
        _tecnicosAtribuidos.Add((userId, chamadoId));
        return this;
    }

    public FakeAuthorizationContext ComMembroDaEquipeResponsavel(Guid userId, Guid chamadoId)
    {
        _membrosDaEquipe.Add((userId, chamadoId));
        return this;
    }

    public FakeAuthorizationContext ComChamadoNaFilaDaEquipe(Guid userId, Guid chamadoId)
    {
        _naFilaDaEquipe.Add((userId, chamadoId));
        return this;
    }

    public Task<bool> EhSolicitanteAsync(Guid userId, Guid chamadoId, CancellationToken cancellationToken) =>
        Task.FromResult(_solicitantes.Contains((userId, chamadoId)));

    public Task<bool> EhTecnicoAtribuidoAsync(Guid userId, Guid chamadoId, CancellationToken cancellationToken) =>
        Task.FromResult(_tecnicosAtribuidos.Contains((userId, chamadoId)));

    public Task<bool> EhMembroDaEquipeResponsavelAsync(Guid userId, Guid chamadoId, CancellationToken cancellationToken) =>
        Task.FromResult(_membrosDaEquipe.Contains((userId, chamadoId)));

    public Task<bool> EstaNaFilaDaEquipeDoUsuarioAsync(Guid userId, Guid chamadoId, CancellationToken cancellationToken) =>
        Task.FromResult(_naFilaDaEquipe.Contains((userId, chamadoId)));
}
