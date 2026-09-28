using Chamados.Application;
using Chamados.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel;
using SharedKernel.Messaging;
using SharedKernel.Modules;

namespace Chamados;

/// <summary>
/// Composição do módulo Chamados (arquitetura/01-estrutura-de-projetos-monolito-modular.md,
/// "Como Api compõe o sistema"). Único ponto do módulo, além da própria Infrastructure
/// interna, que lê IConfiguration diretamente (arquitetura/10). Chamado pela composição raiz
/// (Api/Worker, ainda não implementada nesta rodada):
///
///   builder.Services.AddChamadosModule(builder.Configuration);
///
/// Handlers (Mediator) são descobertos por assembly scanning do próprio pacote
/// Mediator.SourceGenerator, que gera um AddMediator() local a este assembly (namespace
/// Microsoft.Extensions.DependencyInjection) — chamado explicitamente aqui dentro
/// (services.AddMediator(...)), não pela Api (decisão revista nesta rodada: Mediator.SourceGenerator
/// só pode gerar ServiceDescriptor para tipos internal de dentro do PRÓPRIO assembly; a Api
/// nunca referencia Mediator.SourceGenerator, ver nota em Api.csproj). Lifetime Scoped
/// (AssemblyInfo.cs, [assembly: MediatorOptions(ServiceLifetime = ServiceLifetime.Scoped)]):
/// os handlers dependem de serviços Scoped (repositórios, ICurrentUser), então o default
/// Singleton do gerador causaria captive dependency.
///
/// </summary>
public static class ChamadosDependencyInjection
{
    internal const string ModuleKey = "Chamados";

    public static IServiceCollection AddChamadosModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // AddMediator() gerado localmente neste assembly (ver nota da classe acima):
        // registra IMediator/ISender/IPublisher e todo IRequestHandler<,> de
        // Chamados.Application. Lifetime Scoped já fixado em tempo de compilação via
        // [assembly: MediatorOptions(ServiceLifetime = ServiceLifetime.Scoped)] (AssemblyInfo.cs)
        // — o gerador rejeita (MSG0006) configurar o mesmo valor duas vezes (atributo de
        // assembly E delegate aqui), por isso a chamada abaixo é sem argumento.
        services.AddMediator();

        // Rota e portas por módulo (P1/M11 de achados.md): o módulo declara os próprios
        // assemblies e registra IMediator, IUnitOfWork, IIdempotencyStore e IAuthorizationContext
        // keyed pela própria chave. Nada daqui é registrado sem chave, então um segundo módulo com
        // escrita nunca herda o DbContext deste. O ISender composto da Api e os behaviors do
        // SharedKernel resolvem a implementação pelo assembly da mensagem.
        services.AddModuleRoute(
            ModuleKey, typeof(Contracts.AbrirChamadoCommand).Assembly, typeof(ChamadosDependencyInjection).Assembly);
        services.AddKeyedScoped<global::Mediator.IMediator>(
            ModuleKey, (sp, _) => sp.GetRequiredService<global::Mediator.Mediator>());
        services.AddKeyedScoped<IUnitOfWork, ChamadosUnitOfWork>(ModuleKey);
        services.AddKeyedScoped<IIdempotencyStore, IdempotencyStore>(ModuleKey);
        services.AddKeyedScoped<IAuthorizationContext, ChamadoAuthorizationContext>(ModuleKey);

        // Validação sintática (arquitetura/16): sem este registro o ValidationBehavior recebe uma
        // lista vazia e nunca valida nada (C3 de achados.md).
        services.AddValidatorsFromAssembly(typeof(ChamadosDependencyInjection).Assembly, includeInternalTypes: true);

        services
            .AddOptions<ChamadosOptions>()
            .Configure(options => configuration.GetSection(ChamadosOptions.SectionName).Bind(options))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<ChamadosDbContext>((serviceProvider, options) =>
        {
            var chamadosOptions = serviceProvider.GetRequiredService<IOptions<ChamadosOptions>>().Value;
            options.UseSqlite(chamadosOptions.ConnectionString);
            options.AddInterceptors(new RowVersionInterceptor());
        });

        services.AddScoped<IChamadoRepository, ChamadoRepository>();
        services.AddScoped<IChamadoLeitura, ChamadoLeitura>();
        services.AddScoped<IEquipeDoUsuarioResolver, EquipeDoUsuarioResolver>();
        services.AddScoped<IDatabaseInitializer, ChamadosDatabaseInitializer>();

        // Cada camada registra a própria checagem, a Api só agrega em /health
        // (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md, "Health checks e
        // rate limiting" — o exemplo de referência do próprio documento mostra este padrão:
        // cada módulo chama AddHealthChecks().AddDbContextCheck<T>() dentro do próprio método
        // de extensão de DependencyInjection). ChamadosDbContext é internal ao módulo; a Api
        // nunca precisa conhecê-lo para agregar esta checagem em /health.
        services.AddHealthChecks()
            .AddDbContextCheck<ChamadosDbContext>(name: "chamados-db");

        return services;
    }
}
