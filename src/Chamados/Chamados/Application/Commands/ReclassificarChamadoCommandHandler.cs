using Catalogo.Contracts;
using Chamados.Contracts;
using SharedKernel;
using Mediator;

namespace Chamados.Application.Commands;

/// <summary>
/// Reclassifica a prioridade e recalcula o SLA com o snapshot do Catálogo. A autorização
/// (técnico atribuído, ou técnico da equipe com o chamado na fila) roda no pipeline, antes da
/// idempotência (ReclassificarChamadoCommand.IsAuthorizedAsync).
/// </summary>
internal sealed class ReclassificarChamadoCommandHandler : IRequestHandler<ReclassificarChamadoCommand, Result<Unit>>
{
    private readonly IChamadoRepository _repository;
    private readonly ISender _sender;

    public ReclassificarChamadoCommandHandler(IChamadoRepository repository, ISender sender)
    {
        _repository = repository;
        _sender = sender;
    }

    public async ValueTask<Result<Unit>> Handle(ReclassificarChamadoCommand request, CancellationToken cancellationToken)
    {
        var chamado = await _repository.ObterParaEscritaAsync(request.ChamadoId, rowVersionEsperado: null, cancellationToken);
        if (chamado is null)
        {
            return Result<Unit>.NotFound("Chamado não encontrado.");
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
