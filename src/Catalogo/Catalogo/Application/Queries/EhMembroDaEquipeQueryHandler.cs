using Catalogo.Contracts;
using SharedKernel;
using Mediator;

namespace Catalogo.Application.Queries;

/// <summary>
/// Implementação real da Query pública Catalogo.Contracts.EhMembroDaEquipeQuery. Consultada
/// pela implementação de Chamados.Application.IEquipeMembershipChecker (Infrastructure), a
/// partir do guard explícito de ReclassificarChamadoCommandHandler. Checagem determinística
/// de vínculo — sempre Result.Success (verdadeiro ou falso), nunca Failure: "não é membro"
/// não é falha de negócio, é a resposta esperada da consulta.
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class EhMembroDaEquipeQueryHandler : IRequestHandler<EhMembroDaEquipeQuery, Result<bool>>
{
    private readonly IMembroDeEquipeRepository _repository;

    public EhMembroDaEquipeQueryHandler(IMembroDeEquipeRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<Result<bool>> Handle(EhMembroDaEquipeQuery request, CancellationToken cancellationToken)
    {
        var ehMembro = await _repository.EhMembroAsync(request.TecnicoId, request.EquipeId, cancellationToken);

        return Result<bool>.Success(ehMembro);
    }
}
