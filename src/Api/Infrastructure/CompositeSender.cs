using Mediator;
using SharedKernel;

namespace Api.Infrastructure;

/// <summary>
/// ISender composto, único registrado sem chave na composição raiz (o que os controllers
/// injetam). Problema real que motiva esta classe (api-e-composicao): cada módulo
/// (Chamados/Catalogo) referencia Mediator.SourceGenerator no próprio projeto e gera, dentro do
/// próprio assembly, uma classe concreta Mediator.Mediator cujo método de despacho
/// (Send&lt;TResponse&gt;(IRequest&lt;TResponse&gt;, ...)) é um switch fechado em tempo de
/// compilação, conhecendo só os tipos de Command/Query que aquele assembly viu durante a
/// própria geração. Quando a Api compõe os dois módulos no mesmo IServiceCollection, registrar
/// os dois ISender "sem chave" faz o segundo TryAdd (SharedKernelPipeline/gerador usa TryAdd)
/// perder para o primeiro — o Mediator do módulo que perdeu nunca seria resolvido, mesmo tendo
/// registrado os próprios handlers no container, porque o ISender errado lançaria
/// InvalidMessageException para qualquer Command/Query que não é "seu".
///
/// Solução: cada módulo expõe o próprio Mediator.Mediator como serviço keyed
/// (AddKeyedScoped&lt;IMediator&gt;(ModuleSenderKeys.X, ...), ver Chamados/CatalogoDependencyInjection),
/// e este composite roteia pela assembly de declaração do TIPO da mensagem — determinístico,
/// decidido uma vez por tipo (nunca por tentativa/exceção): cada Command/Query pertence a
/// exatamente um module Contracts, então o roteamento nunca é ambíguo.
///
/// Só implementa ISender (não o IMediator completo/IPublisher): nenhum controller desta rodada
/// publica INotification via Api (arquitetura/05, dispatch de evento de domínio é in-process
/// dentro do módulo, não cruza para a Api) — extensão futura se isso mudar.
/// </summary>
internal sealed class CompositeSender : ISender
{
    private readonly IReadOnlyDictionary<string, Func<IServiceProvider, IMediator>> _resolvedByAssemblyName;
    private readonly IServiceProvider _serviceProvider;

    public CompositeSender(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        // Mapa fixo assembly-da-mensagem -> chave do módulo dono. Registrado por nome de
        // assembly (não por instância de Type) porque o mapeamento é sempre "toda mensagem
        // deste assembly de Contracts pertence a este módulo" — nunca precisa granularidade
        // por tipo individual.
        _resolvedByAssemblyName = new Dictionary<string, Func<IServiceProvider, IMediator>>(StringComparer.Ordinal)
        {
            ["Chamados.Contracts"] = sp => sp.GetRequiredKeyedService<IMediator>(ModuleSenderKeys.Chamados),
            ["Catalogo.Contracts"] = sp => sp.GetRequiredKeyedService<IMediator>(ModuleSenderKeys.Catalogo),
        };
    }

    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        ResolverPara(request).Send(request, cancellationToken);

    public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default) =>
        ResolverPara(command).Send(command, cancellationToken);

    public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        ResolverPara(query).Send(query, cancellationToken);

    public ValueTask<object?> Send(object request, CancellationToken cancellationToken = default) =>
        ResolverPara(request).Send(request, cancellationToken);

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default) =>
        ResolverPara(query).CreateStream(query, cancellationToken);

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
        ResolverPara(request).CreateStream(request, cancellationToken);

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default) =>
        ResolverPara(command).CreateStream(command, cancellationToken);

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
        ResolverPara(request).CreateStream(request, cancellationToken);

    private IMediator ResolverPara(object message)
    {
        var nomeDoAssembly = message.GetType().Assembly.GetName().Name;

        if (nomeDoAssembly is null || !_resolvedByAssemblyName.TryGetValue(nomeDoAssembly, out var resolver))
        {
            throw new InvalidOperationException(
                $"Nenhum módulo registrado para despachar mensagens do assembly '{nomeDoAssembly}' " +
                $"(tipo '{message.GetType().FullName}'). Registre a chave em CompositeSender/ModuleSenderKeys " +
                "ao adicionar um novo módulo com Command/Query próprio.");
        }

        return resolver(_serviceProvider);
    }
}
