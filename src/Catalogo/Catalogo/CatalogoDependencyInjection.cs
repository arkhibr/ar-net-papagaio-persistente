using Catalogo.Application;
using Catalogo.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel;
using SharedKernel.Modules;

namespace Catalogo;

/// <summary>
/// Composição do módulo Catalogo (arquitetura/01-estrutura-de-projetos-monolito-modular.md,
/// "Como Api compõe o sistema"). Único ponto do módulo, além da própria Infrastructure interna,
/// que lê IConfiguration diretamente (arquitetura/10-configuracao-e-segredos.md). Chamado pela
/// composição raiz (Api/Worker, ainda não implementada nesta rodada — api-e-composicao):
///
///   builder.Services.AddCatalogoModule(builder.Configuration);
///
/// Handlers (Mediator) são descobertos por assembly scanning do próprio pacote
/// Mediator.SourceGenerator, que gera um AddMediator() local a este assembly — chamado
/// explicitamente aqui dentro (services.AddMediator(...)), não pela Api (mesma decisão
/// documentada em Chamados/ChamadosDependencyInjection.cs: Mediator.SourceGenerator só pode
/// gerar ServiceDescriptor para tipos internal do PRÓPRIO assembly; a Api nunca referencia
/// Mediator.SourceGenerator). Lifetime Scoped (AssemblyInfo.cs).
///
/// IUnitOfWork (e as demais portas do SharedKernel) é registrado keyed pela chave do módulo:
/// o UnitOfWorkBehavior resolve o do módulo dono do Command pelo assembly da mensagem, então
/// Chamados e Catalogo nunca disputam o mesmo registro (P1 de achados.md).
/// </summary>
public static class CatalogoDependencyInjection
{
    internal const string ModuleKey = "Catalogo";

    public static IServiceCollection AddCatalogoModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // AddMediator() gerado localmente neste assembly (ver nota da classe acima). Lifetime
        // Scoped já fixado em tempo de compilação via AssemblyInfo.cs (mesma razão documentada
        // em Chamados/ChamadosDependencyInjection.cs) — chamada sem argumento.
        services.AddMediator();

        // Rota e portas por módulo (P1/M11 de achados.md): mesma disciplina de
        // ChamadosDependencyInjection. Os Commands de administração do Catálogo não usam
        // Idempotency-Key, então o módulo não registra IIdempotencyStore; o primeiro Command que
        // precisar dele falha alto no ModuleService em vez de usar a porta de outro módulo. O
        // IAuthorizationContext existe para a autorização por papel das mensagens de administração.
        services.AddModuleRoute(
            ModuleKey, typeof(Contracts.CategoriasDeServicoQuery).Assembly, typeof(CatalogoDependencyInjection).Assembly);
        services.AddKeyedScoped<global::Mediator.IMediator>(
            ModuleKey, (sp, _) => sp.GetRequiredService<global::Mediator.Mediator>());
        services.AddKeyedScoped<IUnitOfWork, CatalogoUnitOfWork>(ModuleKey);
        services.AddKeyedScoped<IAuthorizationContext, CatalogoAuthorizationContext>(ModuleKey);

        services.AddValidatorsFromAssembly(typeof(CatalogoDependencyInjection).Assembly, includeInternalTypes: true);

        services
            .AddOptions<CatalogoOptions>()
            .Configure(options => configuration.GetSection(CatalogoOptions.SectionName).Bind(options))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<CatalogoDbContext>((serviceProvider, options) =>
        {
            var catalogoOptions = serviceProvider.GetRequiredService<IOptions<CatalogoOptions>>().Value;
            options.UseSqlite(catalogoOptions.ConnectionString);
        });

        services.AddScoped<ICategoriaDeServicoRepository, CategoriaDeServicoRepository>();
        services.AddScoped<IMembroDeEquipeRepository, MembroDeEquipeRepository>();
        services.AddScoped<IEquipeRepository, EquipeRepository>();
        services.AddScoped<IDatabaseInitializer, CatalogoDatabaseInitializer>();

        // Cada camada registra a própria checagem, a Api só agrega em /health
        // (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md, "Health checks e
        // rate limiting"). CatalogoDbContext é internal ao módulo; a Api nunca precisa
        // conhecê-lo para agregar esta checagem em /health.
        services.AddHealthChecks()
            .AddDbContextCheck<CatalogoDbContext>(name: "catalogo-db");

        return services;
    }
}
