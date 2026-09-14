using Catalogo.Contracts;
using SharedKernel;
using Mediator;

namespace Catalogo.Application.Queries;

/// <summary>
/// Implementação real da Query pública Catalogo.Contracts.ResolverEquipeESlaQuery. Resolve
/// a EquipeId a partir da categoria e as horas de SLA a partir da prioridade
/// (Critica 4h, Alta 8h, Media 24h, Baixa 72h — especificacao-clarificada.md).
/// internal: descoberto por DI dentro do próprio assembly, implementa IRequestHandler
/// diretamente (arquitetura/01 — Contracts não expõe handler, nem uma interface derivada dele).
/// </summary>
internal sealed class ResolverEquipeESlaQueryHandler
    : IRequestHandler<ResolverEquipeESlaQuery, Result<ResolverEquipeESlaResultado>>
{
    private readonly ICategoriaDeServicoRepository _repository;

    public ResolverEquipeESlaQueryHandler(ICategoriaDeServicoRepository repository)
    {
        _repository = repository;
    }

    public async ValueTask<Result<ResolverEquipeESlaResultado>> Handle(
        ResolverEquipeESlaQuery request, CancellationToken cancellationToken)
    {
        var categoria = await _repository.ObterPorIdAsync(request.CategoriaId, cancellationToken);
        if (categoria is null)
        {
            return Result<ResolverEquipeESlaResultado>.Failure("Categoria de serviço não encontrada.");
        }

        var horasDeSla = HorasDeSlaPara(request.Prioridade);

        return Result<ResolverEquipeESlaResultado>.Success(
            new ResolverEquipeESlaResultado(categoria.EquipeId, horasDeSla));
    }

    private static int HorasDeSlaPara(PrioridadeServico prioridade) => prioridade switch
    {
        PrioridadeServico.Critica => 4,
        PrioridadeServico.Alta => 8,
        PrioridadeServico.Media => 24,
        PrioridadeServico.Baixa => 72,
        _ => throw new ArgumentOutOfRangeException(nameof(prioridade), prioridade, "Prioridade desconhecida."),
    };
}
