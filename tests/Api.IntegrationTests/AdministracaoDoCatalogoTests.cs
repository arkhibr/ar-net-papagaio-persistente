using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests;

/// <summary>
/// Administração do Catálogo pelo Supervisor: categorias, SLAs, equipes e membros. Categoria
/// nunca é apagada, só inativada (B6 de achados.md).
/// </summary>
public sealed class AdministracaoDoCatalogoTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private HttpClient _supervisor = null!;

    public async Task InitializeAsync() =>
        _supervisor = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Supervisor");

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Guid> CriarEquipeAsync(string nome = "Infraestrutura")
    {
        var resposta = await _supervisor.PostAsJsonAsync("/api/v1/equipes", new { nome });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await ChamadosHttp.JsonAsync(resposta)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CriarCategoriaAsync(Guid equipeId, params (int Prioridade, int Horas)[] slas)
    {
        var resposta = await _supervisor.PostAsJsonAsync("/api/v1/categorias-de-servico", new
        {
            nome = "Rede",
            equipeId,
            slas = slas.Select(s => new { prioridade = s.Prioridade, horas = s.Horas }),
        });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await ChamadosHttp.JsonAsync(resposta)).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement[]> ListarCategoriasAsync(HttpClient cliente, bool incluirInativas = false) =>
        (await ChamadosHttp.JsonAsync(await cliente.GetAsync($"/api/v1/categorias-de-servico?incluirInativas={incluirInativas}")))
        .EnumerateArray().ToArray();

    [Fact]
    public async Task Supervisor_cria_equipe_e_categoria_e_ela_aparece_na_lista_na_hora()
    {
        var solicitante = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        Assert.Empty(await ListarCategoriasAsync(solicitante)); // popula o cache com a lista vazia

        var equipeId = await CriarEquipeAsync();
        var categoriaId = await CriarCategoriaAsync(equipeId, (1, 24), (2, 8));

        var categoria = Assert.Single(await ListarCategoriasAsync(solicitante));
        Assert.Equal(categoriaId, categoria.GetProperty("id").GetGuid());
        Assert.Equal(equipeId, categoria.GetProperty("equipeId").GetGuid());
        Assert.True(categoria.GetProperty("ativa").GetBoolean());
        Assert.Equal(2, categoria.GetProperty("slas").GetArrayLength());

        var equipe = Assert.Single((await ChamadosHttp.JsonAsync(await _supervisor.GetAsync("/api/v1/equipes"))).EnumerateArray());
        Assert.Equal("Infraestrutura", equipe.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Outros_papeis_nao_administram_o_catalogo()
    {
        var tecnico = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Tecnico");

        Assert.Equal(HttpStatusCode.Forbidden, (await tecnico.PostAsJsonAsync("/api/v1/equipes", new { nome = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tecnico.GetAsync("/api/v1/equipes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await tecnico.GetAsync("/api/v1/categorias-de-servico?incluirInativas=true")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tecnico.GetAsync("/api/v1/categorias-de-servico")).StatusCode);
    }

    [Fact]
    public async Task Categoria_com_equipe_inexistente_e_400_de_regra_e_sla_invalido_e_400_de_validacao()
    {
        var semEquipe = await _supervisor.PostAsJsonAsync("/api/v1/categorias-de-servico", new { nome = "Rede", equipeId = Guid.NewGuid(), slas = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.BadRequest, semEquipe.StatusCode);
        Assert.EndsWith("/regra-de-negocio-violada", (await ChamadosHttp.JsonAsync(semEquipe)).GetProperty("type").GetString());

        var equipeId = await CriarEquipeAsync();
        var horasZero = await _supervisor.PostAsJsonAsync("/api/v1/categorias-de-servico", new
        {
            nome = "Rede", equipeId, slas = new[] { new { prioridade = 1, horas = 0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, horasZero.StatusCode);
        Assert.EndsWith("/validacao", (await ChamadosHttp.JsonAsync(horasZero)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task Inativar_tira_a_categoria_da_abertura_mas_chamado_existente_ainda_e_reclassificado()
    {
        var tecnicoId = Guid.NewGuid();
        var equipeId = await CriarEquipeAsync();
        var categoriaId = await CriarCategoriaAsync(equipeId, (1, 24), (2, 8));
        (await _supervisor.PostAsJsonAsync($"/api/v1/equipes/{equipeId}/membros", new { usuarioId = tecnicoId })).EnsureSuccessStatusCode();
        var solicitante = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        var chamadoId = await ChamadosHttp.AbrirAsync(solicitante, categoriaId);

        Assert.Equal(HttpStatusCode.NoContent, (await _supervisor.PostAsync($"/api/v1/categorias-de-servico/{categoriaId}/inativar", null)).StatusCode);

        Assert.Empty(await ListarCategoriasAsync(solicitante));
        var todas = await ListarCategoriasAsync(_supervisor, incluirInativas: true);
        Assert.False(Assert.Single(todas).GetProperty("ativa").GetBoolean());

        var abrir = await solicitante.SendAsync(ChamadosHttp.Post(
            "/api/v1/chamados", new { categoriaId, prioridade = 1 }, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.BadRequest, abrir.StatusCode);

        var tecnico = await _factory.ClienteAutenticadoAsync(tecnicoId, "Tecnico");
        var reclassificar = await tecnico.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/reclassificar", new { novaPrioridade = 2 }, Guid.NewGuid().ToString()));
        // Hoje o reclassificar responde 200 (contratos-front.md diz 204; divergência anterior a esta rodada).
        Assert.True(reclassificar.IsSuccessStatusCode, $"reclassificar: {(int)reclassificar.StatusCode}");

        (await _supervisor.PostAsync($"/api/v1/categorias-de-servico/{categoriaId}/reativar", null)).EnsureSuccessStatusCode();
        await ChamadosHttp.AbrirAsync(solicitante, categoriaId);
    }

    [Fact]
    public async Task Definir_slas_substitui_a_tabela_e_prioridade_retirada_deixa_de_ser_aceita()
    {
        var equipeId = await CriarEquipeAsync();
        var categoriaId = await CriarCategoriaAsync(equipeId, (1, 24), (3, 4));
        var solicitante = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");

        var definir = await _supervisor.PostAsJsonAsync(
            $"/api/v1/categorias-de-servico/{categoriaId}/definir-slas", new { slas = new[] { new { prioridade = 1, horas = 12 } } });
        Assert.Equal(HttpStatusCode.NoContent, definir.StatusCode);

        var critico = await solicitante.SendAsync(ChamadosHttp.Post(
            "/api/v1/chamados", new { categoriaId, prioridade = 3 }, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.BadRequest, critico.StatusCode);
        var sla = Assert.Single(Assert.Single(await ListarCategoriasAsync(solicitante)).GetProperty("slas").EnumerateArray());
        Assert.Equal(12, sla.GetProperty("horas").GetInt32());
    }

    [Fact]
    public async Task Renomear_e_transferir_categoria_e_renomear_equipe()
    {
        var infra = await CriarEquipeAsync("Infra");
        var suporte = await CriarEquipeAsync("Suporte");
        var categoriaId = await CriarCategoriaAsync(infra, (1, 24));

        Assert.Equal(HttpStatusCode.NoContent, (await _supervisor.PostAsJsonAsync($"/api/v1/categorias-de-servico/{categoriaId}/renomear", new { nome = "Redes" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _supervisor.PostAsJsonAsync($"/api/v1/categorias-de-servico/{categoriaId}/transferir", new { equipeId = suporte })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _supervisor.PostAsJsonAsync($"/api/v1/equipes/{infra}/renomear", new { nome = "Infraestrutura" })).StatusCode);

        var categoria = Assert.Single(await ListarCategoriasAsync(_supervisor));
        Assert.Equal("Redes", categoria.GetProperty("nome").GetString());
        Assert.Equal(suporte, categoria.GetProperty("equipeId").GetGuid());
        var nomes = (await ChamadosHttp.JsonAsync(await _supervisor.GetAsync("/api/v1/equipes"))).EnumerateArray().Select(e => e.GetProperty("nome").GetString());
        Assert.Contains("Infraestrutura", nomes);

        Assert.Equal(HttpStatusCode.NotFound, (await _supervisor.PostAsJsonAsync($"/api/v1/categorias-de-servico/{Guid.NewGuid()}/renomear", new { nome = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _supervisor.PostAsJsonAsync($"/api/v1/categorias-de-servico/{categoriaId}/transferir", new { equipeId = Guid.NewGuid() })).StatusCode);
    }

    [Fact]
    public async Task Membro_vinculado_ve_a_fila_nao_entra_em_duas_equipes_e_desvinculado_perde_a_fila()
    {
        var tecnicoId = Guid.NewGuid();
        var infra = await CriarEquipeAsync("Infra");
        var suporte = await CriarEquipeAsync("Suporte");
        var tecnico = await _factory.ClienteAutenticadoAsync(tecnicoId, "Tecnico");
        const string fila = "/api/v1/equipes/fila?page=1&pageSize=10";

        Assert.Equal(HttpStatusCode.BadRequest, (await tecnico.GetAsync(fila)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await _supervisor.PostAsJsonAsync($"/api/v1/equipes/{infra}/membros", new { usuarioId = tecnicoId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await tecnico.GetAsync(fila)).StatusCode);

        var outra = await _supervisor.PostAsJsonAsync($"/api/v1/equipes/{suporte}/membros", new { usuarioId = tecnicoId });
        Assert.Equal(HttpStatusCode.BadRequest, outra.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _supervisor.PostAsJsonAsync($"/api/v1/equipes/{infra}/membros", new { usuarioId = tecnicoId })).StatusCode);

        var membros = (await ChamadosHttp.JsonAsync(await _supervisor.GetAsync("/api/v1/equipes")))
            .EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == infra).GetProperty("membros");
        Assert.Equal(tecnicoId, Assert.Single(membros.EnumerateArray()).GetGuid());

        Assert.Equal(HttpStatusCode.NoContent, (await _supervisor.DeleteAsync($"/api/v1/equipes/{infra}/membros/{tecnicoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _supervisor.DeleteAsync($"/api/v1/equipes/{infra}/membros/{tecnicoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await tecnico.GetAsync(fila)).StatusCode);
    }

    [Fact]
    public async Task Escrita_do_catalogo_exige_csrf()
    {
        _supervisor.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");

        var resposta = await _supervisor.PostAsJsonAsync("/api/v1/equipes", new { nome = "X" });

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.EndsWith("/csrf-invalido", (await ChamadosHttp.JsonAsync(resposta)).GetProperty("type").GetString());
    }
}
