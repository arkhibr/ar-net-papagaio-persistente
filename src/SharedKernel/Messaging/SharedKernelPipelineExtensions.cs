using Mediator;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SharedKernel.Modules;

namespace SharedKernel.Messaging;

/// <summary>
/// Composição raiz dos pipeline behaviors genéricos do SharedKernel
/// (arquitetura/01-estrutura-de-projetos-monolito-modular.md, "Como Api compõe o sistema";
/// arquitetura/03-commands-e-queries.md). Chamado uma única vez pela composição raiz (Api),
/// antes de qualquer Add{Modulo}Module:
///
///   builder.Services
///       .AddSharedKernelPipeline()
///       .AddChamadosModule(builder.Configuration)
///       .AddCatalogoModule(builder.Configuration);
///
/// Registro por open generic (services.AddScoped(typeof(IPipelineBehavior&lt;,&gt;), typeof(X&lt;,&gt;))),
/// não pela lista MediatorOptions.PipelineBehaviors do source generator: os behaviors daqui
/// dependem de serviços Scoped por requisição (ICurrentUser, IUnitOfWork, IIdempotencyStore),
/// e o registro por MediatorOptions é resolvido em tempo de compilação/geração, adequado a
/// cenários Native AOT que esta solution não tem como meta hoje (arquitetura/03, "Biblioteca de
/// Mediator" já registra a preferência por este pacote sem exigir AOT). O README do próprio
/// pacote (Mediator.Abstractions 3.0.2, secao 3.3/4.5) documenta os dois caminhos como
/// equivalentes: "Register as IPipelineBehavior&lt;,&gt; in either case".
///
/// ORDEM DE REGISTRO = ORDEM DE EXECUÇÃO (constatação, não suposição): o container de DI do
/// .NET (Microsoft.Extensions.DependencyInjection) resolve IEnumerable&lt;IPipelineBehavior&lt;,&gt;&gt;
/// na ordem em que cada serviço foi adicionado ao IServiceCollection — comportamento
/// documentado da resolução de coleções do container nativo, não específico deste pacote
/// Mediator. Por isso a ordem das chamadas AddScoped/AddSingleton abaixo é a ordem fixa
/// (arquitetura/03-commands-e-queries.md):
///
///   Logging → Validation → Authorization → Idempotency → Caching → UnitOfWork → Handler
///
/// Um behavior novo entra na posição que a natureza dele exige (ex.: Authorization depois de
/// Validation, antes de Idempotency) — nunca reordenado ad hoc.
/// </summary>
public static class SharedKernelPipelineExtensions
{
    public static IServiceCollection AddSharedKernelPipeline(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.TryAddSingleton<ICacheInvalidator, MemoryCacheInvalidator>();
        services.AddOptions<IdempotencyOptions>();
        services.TryAddSingleton(TimeProvider.System);

        // Portas do SharedKernel resolvidas pelo módulo dono da mensagem (P1/M11 de achados.md):
        // cada Add{Modulo}Module declara os próprios assemblies (AddModuleRoute) e registra as
        // próprias implementações keyed pela chave do módulo.
        services.TryAddSingleton<ModuleRouter>();
        services.TryAddScoped(typeof(IModuleService<>), typeof(ModuleService<>));
        services.TryAddScoped<PendingIdempotency>();

        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        return services;
    }
}
