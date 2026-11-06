using System.Reflection;

namespace SharedKernel.Modules;

/// <summary>
/// Resolve o módulo dono de um tipo de mensagem pelo assembly em que o tipo é declarado,
/// a partir das ModuleRoute registradas. Determinístico e decidido uma vez por assembly.
/// </summary>
public sealed class ModuleRouter
{
    private readonly Dictionary<Assembly, string> _moduloPorAssembly = new();

    public ModuleRouter(IEnumerable<ModuleRoute> rotas)
    {
        foreach (var rota in rotas)
        {
            foreach (var assembly in rota.Assemblies)
            {
                if (_moduloPorAssembly.TryGetValue(assembly, out var existente) && existente != rota.ModuleKey)
                {
                    throw new InvalidOperationException(
                        $"O assembly '{assembly.GetName().Name}' foi registrado por dois módulos ('{existente}' e '{rota.ModuleKey}').");
                }

                _moduloPorAssembly[assembly] = rota.ModuleKey;
            }
        }
    }

    public string ModuleKeyFor(Type messageType)
    {
        if (_moduloPorAssembly.TryGetValue(messageType.Assembly, out var moduleKey))
        {
            return moduleKey;
        }

        throw new InvalidOperationException(
            $"Nenhum módulo registrou o assembly '{messageType.Assembly.GetName().Name}' (tipo '{messageType.FullName}'). " +
            "Todo Add{Modulo}Module precisa chamar AddModuleRoute(...) com os próprios assemblies.");
    }
}
