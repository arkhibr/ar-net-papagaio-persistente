using Catalogo.Contracts;
using SharedKernel;
using Mediator;

namespace Catalogo.Application.Queries;

/// <summary>
/// Snapshot de equipe responsável e SLA (horas) de uma categoria para uma prioridade, consumido
/// por Chamados ao abrir/reclassificar (arquitetura/29 §1). O SLA vem da tabela de referência
/// SlasDeCategoria (M13 de achados.md); categoria sem SLA para a prioridade é falha de negócio,
/// nunca exceção. A categoria inativa não é falha aqui: quem decide é o chamador (a abertura
/// recusa, a reclassificação de um chamado existente aceita).
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
        var categoria = await _repository.ObterEquipeESlaAsync(request.CategoriaId, request.Prioridade, cancellationToken);
        if (categoria is null)
        {
            return Result<ResolverEquipeESlaResultado>.Failure("Categoria de serviço não encontrada.");
        }

        var (equipeId, horasDeSla, ativa) = categoria.Value;
        if (horasDeSla is null)
        {
            return Result<ResolverEquipeESlaResultado>.Failure(
                $"A categoria de serviço não tem SLA definido para a prioridade {request.Prioridade}.");
        }

        return Result<ResolverEquipeESlaResultado>.Success(new ResolverEquipeESlaResultado(equipeId, horasDeSla.Value, ativa));
    }
}
