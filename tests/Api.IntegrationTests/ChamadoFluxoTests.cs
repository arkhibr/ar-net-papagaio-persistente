using System.Net;

namespace Api.IntegrationTests;

/// <summary>Achados A1 (autoatribuição), A3 (fechar/reabrir), M6 (RowVersion/412) e M8 (total) de achados.md.</summary>
public sealed class ChamadoFluxoTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private Cenario _cenario = null!;

    public async Task InitializeAsync() => _cenario = await Cenario.CriarAsync(_factory);

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static async Task<string> EtagAsync(HttpClient cliente, Guid chamadoId)
    {
        var resposta = await cliente.GetAsync($"/api/v1/chamados/{chamadoId}");
        resposta.EnsureSuccessStatusCode();
        return resposta.Headers.ETag!.Tag;
    }

    [Fact]
    public async Task A1_atribuir_sempre_atribui_ao_proprio_tecnico_autenticado()
    {
        var solicitante = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        var chamadoId = await ChamadosHttp.AbrirAsync(solicitante, _cenario.CategoriaId);
        var tecnico = await _factory.ClienteAutenticadoAsync(_cenario.TecnicoId, "Tecnico");

        var resposta = await tecnico.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/atribuir",
            new { tecnicoId = _cenario.OutroTecnicoId },
            Guid.NewGuid().ToString(),
            await EtagAsync(tecnico, chamadoId)));
        Assert.True(resposta.IsSuccessStatusCode, await resposta.Content.ReadAsStringAsync());

        var detalhe = await ChamadosHttp.JsonAsync(await tecnico.GetAsync($"/api/v1/chamados/{chamadoId}"));
        Assert.Equal(_cenario.TecnicoId, detalhe.GetProperty("tecnicoAtribuidoId").GetGuid());
    }

    [Fact]
    public async Task M6_detalhe_nao_expoe_rowVersion_no_corpo_e_ETag_desatualizado_responde_412()
    {
        var solicitante = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        var chamadoId = await ChamadosHttp.AbrirAsync(solicitante, _cenario.CategoriaId);
        var tecnicoA = await _factory.ClienteAutenticadoAsync(_cenario.TecnicoId, "Tecnico");
        var tecnicoB = await _factory.ClienteAutenticadoAsync(_cenario.OutroTecnicoId, "Tecnico");

        var corpo = await ChamadosHttp.JsonAsync(await tecnicoA.GetAsync($"/api/v1/chamados/{chamadoId}"));
        Assert.False(corpo.TryGetProperty("rowVersion", out _));

        var etagAntigo = await EtagAsync(tecnicoA, chamadoId);

        // B atribui e devolve: o chamado volta a Aberto, com outra versão.
        var atribuir = await tecnicoB.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/atribuir", new { }, Guid.NewGuid().ToString(), etagAntigo));
        Assert.True(atribuir.IsSuccessStatusCode, await atribuir.Content.ReadAsStringAsync());
        var devolver = await tecnicoB.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/devolver", null, Guid.NewGuid().ToString()));
        Assert.True(devolver.IsSuccessStatusCode, await devolver.Content.ReadAsStringAsync());

        var resposta = await tecnicoA.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/atribuir", new { }, Guid.NewGuid().ToString(), etagAntigo));

        Assert.Equal(HttpStatusCode.PreconditionFailed, resposta.StatusCode);
    }

    [Fact]
    public async Task M8_totalItems_reflete_a_contagem_real()
    {
        var solicitante = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        for (var i = 0; i < 3; i++)
        {
            await ChamadosHttp.AbrirAsync(solicitante, _cenario.CategoriaId);
        }

        var pagina = await ChamadosHttp.JsonAsync(await solicitante.GetAsync("/api/v1/chamados/meus?page=10&pageSize=20"));

        Assert.Equal(3, pagina.GetProperty("totalItems").GetInt32());
        Assert.Equal(1, pagina.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task A3_solicitante_fecha_o_proprio_chamado_resolvido_e_outro_usuario_nao()
    {
        var solicitanteId = Guid.NewGuid();
        var solicitante = await _factory.ClienteAutenticadoAsync(solicitanteId, "Solicitante");
        var chamadoId = await ChamadosHttp.AbrirAsync(solicitante, _cenario.CategoriaId);
        var tecnico = await _factory.ClienteAutenticadoAsync(_cenario.TecnicoId, "Tecnico");
        (await tecnico.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/atribuir", new { }, Guid.NewGuid().ToString(), await EtagAsync(tecnico, chamadoId))))
            .EnsureSuccessStatusCode();
        (await tecnico.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/resolver", new { notaResolucao = "ok" }, Guid.NewGuid().ToString())))
            .EnsureSuccessStatusCode();

        var intruso = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        var negado = await intruso.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/fechar", null, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.Forbidden, negado.StatusCode);

        var fechado = await solicitante.SendAsync(ChamadosHttp.Post(
            $"/api/v1/chamados/{chamadoId}/fechar", null, Guid.NewGuid().ToString()));
        Assert.True(fechado.IsSuccessStatusCode, await fechado.Content.ReadAsStringAsync());
    }
}
