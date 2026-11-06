using System.Net;
using System.Net.Http.Json;

namespace Api.IntegrationTests;

/// <summary>M1 de achados.md; B1 de adaptacao-bff-angular.md (CSRF em toda operação mutável).</summary>
public sealed class CsrfTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private Cenario _cenario = null!;

    public async Task InitializeAsync() => _cenario = await Cenario.CriarAsync(_factory);

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task B1_post_sem_header_responde_403_csrf_e_nao_reserva_Idempotency_Key()
    {
        var cliente = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        cliente.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        var chave = Guid.NewGuid().ToString();

        var resposta = await cliente.SendAsync(ChamadosHttp.Post(
            "/api/v1/chamados", new { categoriaId = _cenario.CategoriaId, prioridade = 1 }, chave));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var corpo = await ChamadosHttp.JsonAsync(resposta);
        Assert.EndsWith("/csrf-invalido", corpo.GetProperty("type").GetString());
        Assert.Equal(0, _factory.ContarLinhas(_factory.ChamadosDb, "ChamadosIdempotencyRecords"));
    }

    [Fact]
    public async Task B1_post_com_token_invalido_responde_403_csrf()
    {
        var cliente = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        cliente.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        cliente.DefaultRequestHeaders.Add("X-XSRF-TOKEN", "token-invalido");

        var resposta = await cliente.SendAsync(ChamadosHttp.Post(
            "/api/v1/chamados", new { categoriaId = _cenario.CategoriaId, prioridade = 1 }, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.EndsWith("/csrf-invalido", (await ChamadosHttp.JsonAsync(resposta)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task B1_post_com_header_correto_cria_o_chamado_e_get_nunca_exige_token()
    {
        var cliente = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");

        var id = await ChamadosHttp.AbrirAsync(cliente, _cenario.CategoriaId);

        cliente.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/api/v1/chamados/{id}")).StatusCode);
    }

    [Fact]
    public async Task B1_token_emitido_antes_do_login_e_recusado_depois_e_o_reemitido_e_aceito()
    {
        var (cliente, cookies) = _factory.ClienteComCookies();
        (await cliente.GetAsync("/auth/csrf")).EnsureSuccessStatusCode();
        var tokenAnonimo = ApiFactory.Xsrf(cookies);

        (await cliente.PostAsJsonAsync("/dev/login", new { userId = Guid.NewGuid(), roles = new[] { "Solicitante" } }))
            .EnsureSuccessStatusCode();
        cliente.DefaultRequestHeaders.Add("X-XSRF-TOKEN", tokenAnonimo);

        var recusada = await cliente.SendAsync(ChamadosHttp.Post(
            "/api/v1/chamados", new { categoriaId = _cenario.CategoriaId, prioridade = 1 }, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.Forbidden, recusada.StatusCode);

        await ApiFactory.RenovarXsrfAsync(cliente, cookies);
        await ChamadosHttp.AbrirAsync(cliente, _cenario.CategoriaId);
    }
}
