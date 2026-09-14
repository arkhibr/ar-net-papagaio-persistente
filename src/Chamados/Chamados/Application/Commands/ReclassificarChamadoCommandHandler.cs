using Catalogo.Contracts;
using Chamados.Contracts;
using SharedKernel;
using Mediator;

namespace Chamados.Application.Commands;

/// <summary>
/// Carrega o Chamado; guard explícito de autorização (lacuna 1 do plano-de-arquitetura.md):
/// se TecnicoAtribuidoId == currentUser.UserId, autorizado; senão, se Status == Aberto,
/// checa IEquipeMembershipChecker contra a EquipeId do chamado; caso contrário, lança
/// AuthorizationDeniedException (403) sem alterar o agregado. Autorizado, consulta
/// Catalogo.Contracts para o SLA da nova prioridade e chama Chamado.Reclassificar.
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class ReclassificarChamadoCommandHandler : IRequestHandler<ReclassificarChamadoCommand, Result<Unit>>
{
    private readonly IChamadoRepository _repository;
    private readonly IEquipeMembershipChecker _equipeMembershipChecker;
    private readonly ICurrentUser _currentUser;
    private readonly ISender _sender;

    public ReclassificarChamadoCommandHandler(
        IChamadoRepository repository,
        IEquipeMembershipChecker equipeMembershipChecker,
        ICurrentUser currentUser,
        ISender sender)
    {
        _repository = repository;
        _equipeMembershipChecker = equipeMembershipChecker;
        _currentUser = currentUser;
        _sender = sender;
    }

    public async ValueTask<Result<Unit>> Handle(ReclassificarChamadoCommand request, CancellationToken cancellationToken)
    {
        var chamado = await _repository.ObterParaEscritaAsync(request.ChamadoId, rowVersionEsperado: null, cancellationToken);
        if (chamado is null)
        {
            return Result<Unit>.Failure("Chamado não encontrado.");
        }

        var autorizado = chamado.TecnicoAtribuidoId == _currentUser.UserId
            || (chamado.Status == StatusChamado.Aberto
                && await _equipeMembershipChecker.EhMembroDaEquipeAsync(
                    _currentUser.UserId, chamado.EquipeId, cancellationToken));

        if (!autorizado)
        {
            throw new AuthorizationDeniedException(
                "Somente o técnico atribuído, ou um técnico da equipe responsável enquanto o chamado " +
                "ainda está Aberto, pode reclassificar este chamado.");
        }

        var prioridadeServico = (PrioridadeServico)request.NovaPrioridade;
        var resolucao = await _sender.Send(
            new ResolverEquipeESlaQuery(chamado.CategoriaId, prioridadeServico), cancellationToken);

        if (resolucao.IsFailure)
        {
            return Result<Unit>.Failure(resolucao.Error!);
        }

        try
        {
            chamado.Reclassificar(request.NovaPrioridade, resolucao.Value!.HorasDeSla);
        }
        catch (DomainException ex)
        {
            return Result<Unit>.Failure(ex.Message);
        }

        await _repository.SalvarAsync(chamado, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
