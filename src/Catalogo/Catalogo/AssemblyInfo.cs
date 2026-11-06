using Mediator;
using Microsoft.Extensions.DependencyInjection;

// Mesma razão documentada em Chamados/AssemblyInfo.cs: handlers deste módulo dependem de
// serviços Scoped (CatalogoDbContext via ICategoriaDeServicoRepository/IMembroDeEquipeRepository,
// ambos AddScoped em CatalogoDependencyInjection). Default do gerador é Singleton; Scoped
// evita captive dependency.
[assembly: MediatorOptions(ServiceLifetime = ServiceLifetime.Scoped)]
