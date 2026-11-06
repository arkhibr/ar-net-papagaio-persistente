using Chamados.Contracts;
using SharedKernel;
using Mediator;

namespace Chamados.Application.Commands;

/// <summary>Carrega o Chamado, chama Chamado.Devolver, captura DomainException -> Result.Failure, salva. internal (arquitetura/01).</summary>
internal sealed class DevolverChamadoCommandHandler : IRequestHandler<DevolverChamadoCommand, Result<Unit>>
{
    private readonly IChamadoRepository _repository;

    public DevolverChamadoCommandHandler(IChamadoRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<Result<Unit>> Handle(DevolverChamadoCommand request, CancellationToken cancellationToken)
    {
        var chamado = await _repository.ObterParaEscritaAsync(request.ChamadoId, rowVersionEsperado: null, cancellationToken);
        if (chamado is null)
        {
            return Result<Unit>.NotFound("Chamado não encontrado.");
        }

        try
        {
            chamado.Devolver();
        }
        catch (DomainException ex)
        {
            return Result<Unit>.Failure(ex.Message);
        }

        await _repository.SalvarAsync(chamado, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
