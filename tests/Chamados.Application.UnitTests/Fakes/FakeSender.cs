using Catalogo.Contracts;
using Mediator;
using SharedKernel;

namespace Chamados.Application.UnitTests.Fakes;

/// <summary>
/// Fake de ISender usado só para a consulta cross-módulo a Catalogo.Contracts
/// (ResolverEquipeESlaQuery), nunca o módulo Catalogo real (13-estrategia-de-testes.md:
/// "Fakes/mocks das interfaces do módulo"). Implementa a interface ISender completa do
/// pacote Mediator; os overloads de streaming/ICommand/IQuery genéricos não são usados por
/// nenhum handler desta rodada e lançam NotSupportedException se chamados.
/// </summary>
public sealed class FakeSender : ISender
{
    private readonly Dictionary<(Guid CategoriaId, PrioridadeServico Prioridade), ResolverEquipeESlaResultado> _respostas = new();

    public FakeSender ComResposta(Guid categoriaId, PrioridadeServico prioridade, Guid equipeId, int horasDeSla)
    {
        _respostas[(categoriaId, prioridade)] = new ResolverEquipeESlaResultado(equipeId, horasDeSla);
        return this;
    }

    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is ResolverEquipeESlaQuery query)
        {
            var resposta = _respostas.TryGetValue((query.CategoriaId, query.Prioridade), out var resultado)
                ? Result<ResolverEquipeESlaResultado>.Success(resultado)
                : Result<ResolverEquipeESlaResultado>.Failure("Categoria não encontrada.");

            return (ValueTask<TResponse>)(object)new ValueTask<Result<ResolverEquipeESlaResultado>>(resposta);
        }

        throw new NotSupportedException($"FakeSender não sabe responder {request.GetType().Name}.");
    }

    public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeSender não usa ICommand<TResponse> nesta rodada de testes.");

    public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeSender não usa IQuery<TResponse> nesta rodada de testes.");

    public ValueTask<object?> Send(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeSender não usa despacho não genérico nesta rodada de testes.");

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeSender não usa streaming nesta rodada de testes.");

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamCommand<TResponse> command, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeSender não usa streaming nesta rodada de testes.");

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeSender não usa streaming nesta rodada de testes.");

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeSender não usa streaming nesta rodada de testes.");
}
