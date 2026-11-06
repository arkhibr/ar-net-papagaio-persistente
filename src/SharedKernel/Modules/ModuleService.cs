using Microsoft.Extensions.DependencyInjection;

namespace SharedKernel.Modules;

internal sealed class ModuleService<T> : IModuleService<T>
    where T : notnull
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ModuleRouter _router;

    public ModuleService(IServiceProvider serviceProvider, ModuleRouter router)
    {
        _serviceProvider = serviceProvider;
        _router = router;
    }

    public T For(Type messageType)
    {
        var moduleKey = _router.ModuleKeyFor(messageType);

        return _serviceProvider.GetKeyedService<T>(moduleKey)
            ?? throw new InvalidOperationException(
                $"O módulo '{moduleKey}' não registrou {typeof(T).Name} (necessário para '{messageType.Name}'). " +
                $"Registre com AddKeyedScoped<{typeof(T).Name}, ...>(\"{moduleKey}\") no Add{moduleKey}Module.");
    }
}
