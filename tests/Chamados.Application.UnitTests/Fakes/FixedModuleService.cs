using SharedKernel.Modules;

namespace Chamados.Application.UnitTests.Fakes;

/// <summary>IModuleService fake: devolve sempre a mesma instância, qualquer que seja o tipo da mensagem.</summary>
public sealed class FixedModuleService<T> : IModuleService<T>
    where T : notnull
{
    private readonly T _instancia;

    public FixedModuleService(T instancia)
    {
        _instancia = instancia;
    }

    public T For(Type messageType) => _instancia;
}
