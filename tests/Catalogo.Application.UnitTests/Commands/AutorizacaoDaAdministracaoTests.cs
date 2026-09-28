using Catalogo.Contracts;
using SharedKernel;
using Xunit;

namespace Catalogo.Application.UnitTests.Commands;

/// <summary>
/// Item 8 das melhorias: a administração do Catálogo confere o papel na própria mensagem, para
/// qualquer chamador de Contracts, e não só no [Authorize(Roles)] da Api.
/// </summary>
public class AutorizacaoDaAdministracaoTests
{
    private sealed class UsuarioDeTeste(bool autenticado, params string[] papeis) : ICurrentUser
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid SessionId => Guid.Empty;
        public bool IsAuthenticated => autenticado;
        public bool IsSystemActor => false;
        public bool IsInRole(string role) => papeis.Contains(role);
    }

    private sealed class Contexto : ICatalogoAuthorizationContext;

    public static TheoryData<IRequiresAuthorization> MensagensDeAdministracao() =>
    [
        new CriarCategoriaDeServicoCommand("Rede", Guid.NewGuid(), []),
        new RenomearCategoriaDeServicoCommand(Guid.NewGuid(), "Rede"),
        new TransferirCategoriaDeServicoCommand(Guid.NewGuid(), Guid.NewGuid()),
        new DefinirSlasDaCategoriaCommand(Guid.NewGuid(), []),
        new InativarCategoriaDeServicoCommand(Guid.NewGuid()),
        new ReativarCategoriaDeServicoCommand(Guid.NewGuid()),
        new CriarEquipeCommand("Infra"),
        new RenomearEquipeCommand(Guid.NewGuid(), "Infra"),
        new VincularMembroDaEquipeCommand(Guid.NewGuid(), Guid.NewGuid()),
        new DesvincularMembroDaEquipeCommand(Guid.NewGuid(), Guid.NewGuid()),
        new EquipesQuery(),
        new CategoriasDeServicoQuery(IncluirInativas: true),
    ];

    private static Task<bool> AutorizadoAsync(IRequiresAuthorization mensagem, ICurrentUser usuario) =>
        mensagem.IsAuthorizedAsync(usuario, new Contexto(), CancellationToken.None);

    [Theory]
    [MemberData(nameof(MensagensDeAdministracao))]
    public async Task Supervisor_e_autorizado(IRequiresAuthorization mensagem)
    {
        Assert.True(await AutorizadoAsync(mensagem, new UsuarioDeTeste(true, PapeisDoCatalogo.Administrador)));
    }

    [Theory]
    [MemberData(nameof(MensagensDeAdministracao))]
    public async Task Outros_papeis_e_anonimo_nao_sao_autorizados(IRequiresAuthorization mensagem)
    {
        Assert.False(await AutorizadoAsync(mensagem, new UsuarioDeTeste(true, "Tecnico", "Solicitante")));
        Assert.False(await AutorizadoAsync(mensagem, new UsuarioDeTeste(false, PapeisDoCatalogo.Administrador)));
    }

    [Fact]
    public async Task Listar_so_as_ativas_vale_para_qualquer_autenticado()
    {
        var query = new CategoriasDeServicoQuery();

        Assert.True(await AutorizadoAsync(query, new UsuarioDeTeste(true, "Solicitante")));
        Assert.False(await AutorizadoAsync(query, new UsuarioDeTeste(false)));
    }
}
