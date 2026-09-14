using Chamados.Contracts;
using SharedKernel;
using Mediator;

namespace Chamados.Application.Queries;

/// <summary>
/// Carrega o Chamado (leitura não rastreada, ObterSomenteLeituraAsync), guard explícito de
/// autorização (ver ObterChamadoQuery): solicitante dono, técnico atualmente atribuído, ou
/// membro (qualquer papel Tecnico/Supervisor) da equipe responsável, em qualquer status.
/// Busca também o RowVersion atual (ObterRowVersionAsync) para ChamadoDetalheDto.RowVersion,
/// que a Api usa para montar o header ETag (arquitetura/06).
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class ObterChamadoQueryHandler : IRequestHandler<ObterChamadoQuery, Result<ChamadoDetalheDto>>
{
    private readonly IChamadoRepository _repository;
    private readonly IEquipeMembershipChecker _equipeMembershipChecker;
    private readonly ICurrentUser _currentUser;

    public ObterChamadoQueryHandler(
        IChamadoRepository repository,
        IEquipeMembershipChecker equipeMembershipChecker,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _equipeMembershipChecker = equipeMembershipChecker;
        _currentUser = currentUser;
    }

    public async ValueTask<Result<ChamadoDetalheDto>> Handle(ObterChamadoQuery request, CancellationToken cancellationToken)
    {
        var chamado = await _repository.ObterSomenteLeituraAsync(request.ChamadoId, cancellationToken);
        if (chamado is null)
        {
            return Result<ChamadoDetalheDto>.Failure("Chamado não encontrado.");
        }

        var autorizado = chamado.SolicitanteId == _currentUser.UserId
            || chamado.TecnicoAtribuidoId == _currentUser.UserId
            || ((_currentUser.IsInRole("Tecnico") || _currentUser.IsInRole("Supervisor"))
                && await _equipeMembershipChecker.EhMembroDaEquipeAsync(
                    _currentUser.UserId, chamado.EquipeId, cancellationToken));

        if (!autorizado)
        {
            throw new AuthorizationDeniedException(
                "Somente o solicitante, o técnico atribuído, ou um técnico/supervisor da equipe " +
                "responsável pode ver o detalhe deste chamado.");
        }

        var rowVersion = await _repository.ObterRowVersionAsync(request.ChamadoId, cancellationToken);

        var detalhe = new ChamadoDetalheDto(
            chamado.Id,
            chamado.SolicitanteId,
            chamado.CategoriaId,
            chamado.EquipeId,
            chamado.Prioridade,
            chamado.Status,
            chamado.AbertoEm,
            chamado.PrazoSla,
            chamado.TecnicoAtribuidoId,
            chamado.NotaResolucao,
            chamado.ResolvidoEm,
            chamado.FechadoEm,
            chamado.Escalonado,
            chamado.DataEscalonamento,
            Convert.ToBase64String(rowVersion ?? []));

        return Result<ChamadoDetalheDto>.Success(detalhe);
    }
}
