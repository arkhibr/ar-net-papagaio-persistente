using Mediator;
using SharedKernel.Modules;

namespace Api.Infrastructure;

/// <summary>
/// ISender único que os controllers (e os módulos, para consultas entre módulos) injetam.
/// Cada módulo gera o próprio Mediator.Mediator (Mediator.SourceGenerator local ao assembly), que
/// só despacha os tipos que aquele assembly viu na geração; este composite entrega cada mensagem
/// ao IMediator do módulo dono, descoberto pelo assembly do tipo a partir das rotas que cada
/// Add{Modulo}Module registrou (AddModuleRoute). Acrescentar um módulo não exige mexer aqui nem
/// no SharedKernel (M11 de achados.md).
///
/// Só implementa ISender: nenhum ponto da Api publica INotification (publisher composto é o P2
/// de achados.md).
/// </summary>
internal sealed class CompositeSender : ISender
{
    private readonly IModuleService<IMediator> _mediators;

    public CompositeSender(IModuleService<IMediator> mediators)
    {
        _mediators = mediators;
    }

    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        Para(request).Send(request, cancellationToken);

    public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) =>
        Para(command).Send(command, cancellationToken);

    public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        Para(query).Send(query, cancellationToken);

    public ValueTask<object?> Send(object request, CancellationToken cancellationToken = default) =>
        Para(request).Send(request, cancellationToken);

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        Para(query).CreateStream(query, cancellationToken);

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        Para(request).CreateStream(request, cancellationToken);

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default) =>
        Para(command).CreateStream(command, cancellationToken);

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
        Para(request).CreateStream(request, cancellationToken);

    private IMediator Para(object message) => _mediators.For(message.GetType());
}
