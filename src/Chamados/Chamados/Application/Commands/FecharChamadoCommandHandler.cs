using Chamados.Contracts;
using SharedKernel;
using Mediator;

namespace Chamados.Application.Commands;

/// <summary>Carrega o Chamado, chama Chamado.Fechar, captura DomainException -> Result.Failure, salva. internal (arquitetura/01).</summary>
internal sealed class FecharChamadoCommandHandler : IRequestHandler<FecharChamadoCommand, Result<Unit>>
{
    private readonly IChamadoRepository _repository;
    private readonly TimeProvider _timeProvider;

    public FecharChamadoCommandHandler(IChamadoRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async ValueTask<Result<Unit>> Handle(FecharChamadoCommand request, CancellationToken cancellationToken)
    {
        var chamado = await _repository.ObterParaEscritaAsync(request.ChamadoId, rowVersionEsperado: null, cancellationToken);
        if (chamado is null)
        {
            return Result<Unit>.Failure("Chamado não encontrado.");
        }

        try
        {
            chamado.Fechar(_timeProvider.GetUtcNow());
        }
        catch (DomainException ex)
        {
            return Result<Unit>.Failure(ex.Message);
        }

        await _repository.SalvarAsync(chamado, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
