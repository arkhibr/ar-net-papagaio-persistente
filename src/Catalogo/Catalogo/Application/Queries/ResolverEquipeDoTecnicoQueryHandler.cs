using Catalogo.Contracts;
using SharedKernel;
using Mediator;

namespace Catalogo.Application.Queries;

/// <summary>
/// Implementação real da Query pública Catalogo.Contracts.ResolverEquipeDoTecnicoQuery.
/// Consultada pela implementação de Chamados.Application.IEquipeDoUsuarioResolver
/// (Infrastructure), usada por FilaDaEquipeQueryHandler para resolver a equipe do
/// técnico/supervisor autenticado (filtro por linha, nunca EquipeId vinda do cliente).
/// Ausência de vínculo é Value = null, não Result.Failure: quem decide o que fazer com a
/// ausência (ex.: "usuário não vinculado a nenhuma equipe") é o handler chamador.
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class ResolverEquipeDoTecnicoQueryHandler
    : IRequestHandler<ResolverEquipeDoTecnicoQuery, Result<Guid?>>
{
    private readonly IMembroDeEquipeRepository _repository;

    public ResolverEquipeDoTecnicoQueryHandler(IMembroDeEquipeRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<Result<Guid?>> Handle(
        ResolverEquipeDoTecnicoQuery request, CancellationToken cancellationToken)
    {
        var equipeId = await _repository.ResolverEquipeIdAsync(request.TecnicoId, cancellationToken);

        return Result<Guid?>.Success(equipeId);
    }
}
