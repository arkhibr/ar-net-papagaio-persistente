using Chamados.Contracts;
using SharedKernel;
using Mediator;

namespace Chamados.Application.Commands;

/// <summary>
/// Carrega o Chamado via ObterParaEscritaAsync, repassando request.RowVersion (decodificado
/// pelo controller a partir do header If-Match) para a checagem de concorrência otimista contra
/// o valor que o cliente enviou, não só o valor lido do banco no load (arquitetura/06;
/// ChamadoRepository.ObterParaEscritaAsync). Chama Chamado.Atribuir, captura DomainException ->
/// Result.Failure, salva. Conflito de RowVersion não é tratado aqui: propaga como
/// ConcurrencyException a partir do SaveChanges do UnitOfWorkBehavior.
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class AtribuirChamadoCommandHandler : IRequestHandler<AtribuirChamadoCommand, Result<Unit>>
{
    private readonly IChamadoRepository _repository;

    public AtribuirChamadoCommandHandler(IChamadoRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<Result<Unit>> Handle(AtribuirChamadoCommand request, CancellationToken cancellationToken)
    {
        var chamado = await _repository.ObterParaEscritaAsync(request.ChamadoId, request.RowVersion, cancellationToken);
        if (chamado is null)
        {
            return Result<Unit>.Failure("Chamado não encontrado.");
        }

        try
        {
            chamado.Atribuir(request.TecnicoId);
        }
        catch (DomainException ex)
        {
            return Result<Unit>.Failure(ex.Message);
        }

        await _repository.SalvarAsync(chamado, cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
