using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Modules;

public static class ModuleRegistrationExtensions
{
    /// <summary>Declara os assemblies do módulo (ver ModuleRoute). Chamado uma vez por Add{Modulo}Module.</summary>
    public static IServiceCollection AddModuleRoute(
        this IServiceCollection services, string moduleKey, params Assembly[] assemblies)
    {
        services.AddSingleton(new ModuleRoute(moduleKey, assemblies));
        return services;
    }
}
