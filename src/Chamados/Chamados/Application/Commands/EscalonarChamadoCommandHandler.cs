using Chamados.Contracts;
using SharedKernel;
using Mediator;

namespace Chamados.Application.Commands;

/// <summary>Carrega o Chamado, chama Chamado.Escalonar, captura DomainException -> Result.Failure, salva. internal (arquitetura/01).</summary>
internal sealed class EscalonarChamadoCommandHandler : IRequestHandler<EscalonarChamadoCommand, Result<Unit>>
{
    private readonly IChamadoRepository _repository;
    private readonly TimeProvider _timeProvider;

    public EscalonarChamadoCommandHandler(IChamadoRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async ValueTask<Result<Unit>> Handle(EscalonarChamadoCommand request, CancellationToken cancellationToken)
    {
        var chamado = await _repository.ObterParaEscritaAsync(request.ChamadoId, rowVersionEsperado: null, cancellationToken);
        if (chamado is null)
        {
            return Result<Unit>.NotFound("Chamado não encontrado.");
        }

        try
        {
            chamado.Escalonar(_timeProvider.GetUtcNow());
        }
        catch (DomainException ex)
        {
            return Result<Unit>.Failure(ex.Message);
        }

        await _repository.SalvarAsync(chamado, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
