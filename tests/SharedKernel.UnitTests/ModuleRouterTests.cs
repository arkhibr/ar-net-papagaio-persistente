using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Messaging;
using SharedKernel.Modules;
using Xunit;

namespace SharedKernel.UnitTests;

/// <summary>
/// Portas do SharedKernel resolvidas pelo módulo dono da mensagem (P1/M11 de achados.md). Aqui
/// dois "módulos" fictícios: o assembly deste projeto de teste e o do SharedKernel.
/// </summary>
public class ModuleRouterTests
{
    private sealed class UnitOfWorkA : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public void DiscardChanges() { }
    }

    private sealed class UnitOfWorkB : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public void DiscardChanges() { }
    }

    private static readonly Assembly ModuloA = typeof(ModuleRouterTests).Assembly;
    private static readonly Assembly ModuloB = typeof(Result).Assembly;

    private static ServiceProvider Container(bool registrarB = true)
    {
        var services = new ServiceCollection();
        services.AddSharedKernelPipeline();
        services.AddModuleRoute("A", ModuloA);
        services.AddKeyedScoped<IUnitOfWork, UnitOfWorkA>("A");
        if (registrarB)
        {
            services.AddModuleRoute("B", ModuloB);
            services.AddKeyedScoped<IUnitOfWork, UnitOfWorkB>("B");
        }

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Cada_mensagem_recebe_o_IUnitOfWork_do_proprio_modulo()
    {
        using var provider = Container();
        using var scope = provider.CreateScope();
        var unitsOfWork = scope.ServiceProvider.GetRequiredService<IModuleService<IUnitOfWork>>();

        Assert.IsType<UnitOfWorkA>(unitsOfWork.For(typeof(ModuleRouterTests)));
        Assert.IsType<UnitOfWorkB>(unitsOfWork.For(typeof(Result)));
    }

    [Fact]
    public void Mensagem_de_assembly_sem_rota_falha_alto()
    {
        using var provider = Container(registrarB: false);
        using var scope = provider.CreateScope();
        var unitsOfWork = scope.ServiceProvider.GetRequiredService<IModuleService<IUnitOfWork>>();

        Assert.Throws<InvalidOperationException>(() => unitsOfWork.For(typeof(Result)));
    }

    [Fact]
    public void Modulo_sem_a_porta_registrada_falha_alto_em_vez_de_usar_a_de_outro_modulo()
    {
        using var provider = Container();
        using var scope = provider.CreateScope();
        var stores = scope.ServiceProvider.GetRequiredService<IModuleService<IIdempotencyStore>>();

        var erro = Assert.Throws<InvalidOperationException>(() => stores.For(typeof(ModuleRouterTests)));
        Assert.Contains("'A'", erro.Message);
    }
}
