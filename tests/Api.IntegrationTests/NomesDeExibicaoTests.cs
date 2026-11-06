using System.Net;
using System.Net.Http.Json;
using Api.Bff;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

/// <summary>Item 5: nome de exibição do usuário (BFF) e da equipe (Catálogo) em vez de GUID.</summary>
public sealed class NomesDeExibicaoTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> EntrarAsync(Guid userId, string papel, string? nome = null)
    {
        var (cliente, cookies) = _factory.ClienteComCookies();
        (await cliente.PostAsJsonAsync("/dev/login", new { userId, roles = new[] { papel }, nome })).EnsureSuccessStatusCode();
        await ApiFactory.RenovarXsrfAsync(cliente, cookies);
        return cliente;
    }

    [Fact]
    public async Task Me_traz_o_nome_definido_no_login_de_desenvolvimento()
    {
        var cliente = await EntrarAsync(Guid.NewGuid(), "Solicitante", "Maria Silva");

        var me = await ChamadosHttp.JsonAsync(await cliente.GetAsync("/api/me"));

        Assert.Equal("Maria Silva", me.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Criar_usuario_local_com_nome_aparece_no_me()
    {
        string senha;
        using (var scope = _factory.Services.CreateScope())
        {
            (_, senha) = await scope.ServiceProvider.GetRequiredService<CadastroDeUsuarios>()
                .CriarUsuarioLocalAsync("joana", ["Tecnico"], "Joana Prado", CancellationToken.None);
        }

        var (cliente, _) = _factory.ClienteComCookies();
        (await BffHttp.PostarLoginAsync(cliente, "joana", senha)).EnsureRedirect();

        Assert.Equal("Joana Prado", (await ChamadosHttp.JsonAsync(await cliente.GetAsync("/api/me"))).GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Nomes_devolve_so_os_ids_pedidos_e_desconhecido_vem_com_nome_null()
    {
        var tecnico = Guid.NewGuid();
        await EntrarAsync(tecnico, "Tecnico", "Carlos Técnico");
        await EntrarAsync(Guid.NewGuid(), "Tecnico", "Outro");
        var desconhecido = Guid.NewGuid();
        var solicitante = await EntrarAsync(Guid.NewGuid(), "Solicitante");

        var nomes = (await ChamadosHttp.JsonAsync(await solicitante.GetAsync($"/api/v1/usuarios/nomes?ids={tecnico}&ids={desconhecido}")))
            .EnumerateArray().ToDictionary(n => n.GetProperty("id").GetGuid(), n => n.GetProperty("nome").GetString());

        Assert.Equal(2, nomes.Count);
        Assert.Equal("Carlos Técnico", nomes[tecnico]);
        Assert.Null(nomes[desconhecido]);
    }

    [Fact]
    public async Task Nomes_recusa_mais_de_100_ids()
    {
        var cliente = await EntrarAsync(Guid.NewGuid(), "Solicitante");
        var query = string.Join('&', Enumerable.Range(0, 101).Select(_ => $"ids={Guid.NewGuid()}"));

        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.GetAsync($"/api/v1/usuarios/nomes?{query}")).StatusCode);
    }

    [Fact]
    public async Task Lista_completa_de_usuarios_e_so_do_supervisor_e_traz_papeis()
    {
        var tecnico = Guid.NewGuid();
        await EntrarAsync(tecnico, "Tecnico", "Carlos");
        var supervisor = await EntrarAsync(Guid.NewGuid(), "Supervisor", "Sara");
        var solicitante = await EntrarAsync(Guid.NewGuid(), "Solicitante");

        Assert.Equal(HttpStatusCode.Forbidden, (await solicitante.GetAsync("/api/v1/usuarios")).StatusCode);

        var usuarios = (await ChamadosHttp.JsonAsync(await supervisor.GetAsync("/api/v1/usuarios"))).EnumerateArray().ToArray();
        var carlos = usuarios.Single(u => u.GetProperty("id").GetGuid() == tecnico);
        Assert.Equal("Carlos", carlos.GetProperty("nome").GetString());
        Assert.Equal(["Tecnico"], carlos.GetProperty("papeis").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task Categoria_traz_o_nome_da_equipe()
    {
        var supervisor = await EntrarAsync(Guid.NewGuid(), "Supervisor");
        var equipe = (await ChamadosHttp.JsonAsync(await supervisor.PostAsJsonAsync("/api/v1/equipes", new { nome = "Infraestrutura" })))
            .GetProperty("id").GetGuid();
        (await supervisor.PostAsJsonAsync("/api/v1/categorias-de-servico", new { nome = "Rede", equipeId = equipe, slas = new[] { new { prioridade = 1, horas = 24 } } }))
            .EnsureSuccessStatusCode();

        var categoria = Assert.Single((await ChamadosHttp.JsonAsync(await supervisor.GetAsync("/api/v1/categorias-de-servico"))).EnumerateArray());

        Assert.Equal("Infraestrutura", categoria.GetProperty("equipeNome").GetString());
    }
}
