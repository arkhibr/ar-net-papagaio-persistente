using Catalogo.Application;
using Catalogo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel;

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
/// Nota sobre IUnitOfWork (api-e-composicao, colisão de DI resolvida nesta rodada):
/// IUnitOfWork (SharedKernel) é uma porta única, mas Chamados e Catalogo têm implementações
/// concretas distintas (ChamadosUnitOfWork, CatalogoUnitOfWork), cada uma commitando no
/// próprio DbContext do módulo. Se a composição raiz (Api) registrar os dois módulos no mesmo
/// IServiceCollection e cada um chamar AddScoped&lt;IUnitOfWork, X&gt;() simples, o último
/// registro vence silenciosamente — errado, porque UnitOfWorkBehavior (SharedKernel.Messaging)
/// resolveria sempre a implementação do módulo registrado por último, não a do módulo dono do
/// Command em execução.
///
/// Catalogo não tem, hoje, nenhum Command transacional (só Queries, plano-de-arquitetura.md
/// secao 5 — nenhum Command aparece para Catalogo), então a colisão não é um problema real
/// ainda: não registrar IUnitOfWork aqui não quebra nada, porque UnitOfWorkBehavior só roda
/// para TMessage : ITransactionalCommand, e Catalogo não expõe nenhum. Por isso o registro de
/// IUnitOfWork foi removido deste método — mesma disciplina de não adicionar
/// abstração/registro para caso hipotético já seguida nesta solution (ex.: Catalogo permanece
/// anêmico até ter sinal real de promoção, arquitetura/01/plano-de-arquitetura.md secao 2).
///
/// Volta a ser um problema real no dia em que Catalogo ganhar seu primeiro Command transacional
/// (ex.: cadastro de CategoriaDeServico/MembroDeEquipe). Solução futura mais provável, não
/// implementada agora por não haver consumidor: registrar cada IUnitOfWork por
/// AddKeyedScoped&lt;IUnitOfWork&gt;(nomeDoModulo, ...) e o UnitOfWorkBehavior resolver a chave
/// certa a partir do assembly de TMessage (via IServiceProvider/reflexão) — mecanismo novo, só
/// justificado quando existir um segundo módulo com Command real disputando o mesmo tipo.
/// CatalogoUnitOfWork (Infrastructure) continua existindo e implementada, só não registrada
/// aqui: reativar o registro (com a chave, quando keyed services existir) é o primeiro passo
/// dessa migração futura.
/// </summary>
public static class CatalogoDependencyInjection
{
    public static IServiceCollection AddCatalogoModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        // AddMediator() gerado localmente neste assembly (ver nota da classe acima). Lifetime
        // Scoped já fixado em tempo de compilação via AssemblyInfo.cs (mesma razão documentada
        // em Chamados/ChamadosDependencyInjection.cs) — chamada sem argumento.
        services.AddMediator();

        // Registro por CHAVE, mesma razão documentada em ChamadosDependencyInjection.cs: o
        // Mediator.Mediator gerado por Catalogo só despacha os tipos que este assembly viu na
        // própria geração; a Api monta um ISender composto (Api/Infrastructure/CompositeSender.cs)
        // que roteia por esta chave.
        services.AddKeyedScoped<global::Mediator.IMediator>(
            ModuleSenderKeys.Catalogo, (sp, _) => sp.GetRequiredService<global::Mediator.Mediator>());

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
