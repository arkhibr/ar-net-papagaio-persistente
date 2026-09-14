using Chamados.Application;
using Chamados.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel;
using SharedKernel.Messaging;

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
/// Nota de escopo: IAuthorizationContext é porta de SharedKernel (não de Chamados.Application),
/// mas a implementação real (ChamadoAuthorizationContext) é específica deste módulo — por
/// isso é registrada aqui, na composição do módulo Chamados, e não em algum lugar
/// "compartilhado". Se um segundo módulo precisar de IAuthorizationContext no futuro, cada um
/// registra a própria implementação (o container de DI resolve por Scoped normalmente dentro
/// do próprio módulo que a consome).
/// </summary>
public static class ChamadosDependencyInjection
{
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

        // Registro adicional por CHAVE (api-e-composicao, problema real encontrado montando a
        // Api): o Mediator.Mediator gerado por este assembly só sabe despachar (switch
        // compile-time) os tipos de Command/Query que ESTE assembly viu durante a própria
        // geração (Chamados.Contracts + o que Chamados despacha de Catalogo.Contracts). Quando
        // a Api compõe os dois módulos no mesmo IServiceCollection, o ISender/IMediator "sem
        // chave" (services.TryAdd) fica só com o Mediator do módulo cujo AddMediator() rodou
        // primeiro — o outro módulo nunca teria seu ISender resolvido, mesmo com os handlers
        // dele presentes no container (o Mediator errado lançaria InvalidMessageException para
        // qualquer tipo que não é "seu"). Cada módulo expõe o próprio IMediator por uma chave
        // estável (nome do módulo) para a Api montar um ISender composto (Api/Infrastructure/
        // CompositeSender.cs) que roteia pelo assembly do tipo da mensagem — sem isso, um dos
        // dois módulos ficaria inacessível via ISender assim que o outro também fosse composto.
        services.AddKeyedScoped<global::Mediator.IMediator>(
            ModuleSenderKeys.Chamados, (sp, _) => sp.GetRequiredService<global::Mediator.Mediator>());

        services
            .AddOptions<ChamadosOptions>()
            .Configure(options => configuration.GetSection(ChamadosOptions.SectionName).Bind(options))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<ChamadosDbContext>((serviceProvider, options) =>
        {
            var chamadosOptions = serviceProvider.GetRequiredService<IOptions<ChamadosOptions>>().Value;
            options.UseSqlite(chamadosOptions.ConnectionString);
        });

        services.AddScoped<IChamadoRepository, ChamadoRepository>();
        services.AddScoped<IUnitOfWork, ChamadosUnitOfWork>();
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();
        services.AddScoped<IEquipeMembershipChecker, EquipeMembershipChecker>();
        services.AddScoped<IEquipeDoUsuarioResolver, EquipeDoUsuarioResolver>();
        services.AddScoped<IAuthorizationContext, ChamadoAuthorizationContext>();
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
