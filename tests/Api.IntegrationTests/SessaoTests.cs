using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

/// <summary>
/// M2 e B9 de achados.md; B2 (GET /api/me), B4 (/auth/logout), B6 (Data Protection), B7
/// (AuthSession) e B8 (papéis no servidor) de adaptacao-bff-angular.md.
/// </summary>
public sealed class SessaoTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static string Id(Guid id) => id.ToString().ToUpperInvariant();

    [Fact]
    public async Task B2_me_autenticado_devolve_os_papeis_e_emite_o_XSRF_TOKEN()
    {
        var userId = Guid.NewGuid();
        var (cliente, _) = _factory.ClienteComCookies();
        (await cliente.PostAsJsonAsync("/dev/login", new { userId, roles = new[] { "Tecnico", "Supervisor" } }))
            .EnsureSuccessStatusCode();

        var resposta = await cliente.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await ChamadosHttp.JsonAsync(resposta);
        Assert.Equal(userId, corpo.GetProperty("userId").GetGuid());
        Assert.Equal(["Tecnico", "Supervisor"], corpo.GetProperty("papeis").EnumerateArray().Select(p => p.GetString()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, corpo.GetProperty("nome").ValueKind);
        Assert.True(corpo.TryGetProperty("sessaoExpiraEm", out _));
        Assert.False(corpo.TryGetProperty("sid", out _));
        Assert.Contains(resposta.Headers.GetValues("Set-Cookie"), c => c.StartsWith("XSRF-TOKEN=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task B2_me_anonimo_responde_401_em_problem_json()
    {
        var resposta = await _factory.CreateClient().GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        Assert.EndsWith("/nao-autenticado", (await ChamadosHttp.JsonAsync(resposta)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task B4_logout_sem_csrf_responde_403_e_com_csrf_encerra_a_sessao()
    {
        var cliente = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        var token = cliente.DefaultRequestHeaders.GetValues("X-XSRF-TOKEN").Single();

        cliente.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsync("/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/me")).StatusCode);

        cliente.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
        var logout = await cliente.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal("/auth/login", (await ChamadosHttp.JsonAsync(logout)).GetProperty("redirectUrl").GetString());

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task B7_logout_revoga_a_sessao_no_servidor_mesmo_que_o_cookie_seja_reapresentado()
    {
        var (cliente, cookies) = _factory.ClienteComCookies();
        (await cliente.PostAsJsonAsync("/dev/login", new { userId = Guid.NewGuid(), roles = new[] { "Solicitante" } }))
            .EnsureSuccessStatusCode();
        var cookieDeSessao = cookies.GetCookieHeader(new Uri("http://localhost"));
        await ApiFactory.RenovarXsrfAsync(cliente, cookies);

        (await cliente.PostAsync("/auth/logout", null)).EnsureSuccessStatusCode();

        // Um cliente que guardou o cookie antigo não consegue reaproveitá-lo.
        var copia = _factory.CreateClient();
        copia.DefaultRequestHeaders.Add("Cookie", cookieDeSessao);
        Assert.Equal(HttpStatusCode.Unauthorized, (await copia.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task B7_sessao_revogada_no_banco_responde_401_mesmo_com_cookie_valido()
    {
        var userId = Guid.NewGuid();
        var cliente = await _factory.ClienteAutenticadoAsync(userId, "Solicitante");

        _factory.ExecutarSql(_factory.BffDb, "UPDATE AuthSessions SET RevokedAt = '2026-01-01 00:00:00' WHERE UserId = $id", ("$id", Id(userId)));

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task B7_incrementar_SecurityVersion_derruba_todas_as_sessoes_do_usuario()
    {
        var userId = Guid.NewGuid();
        var primeira = await _factory.ClienteAutenticadoAsync(userId, "Solicitante");
        var segunda = await _factory.ClienteAutenticadoAsync(userId, "Solicitante");
        var outro = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");

        _factory.ExecutarSql(_factory.BffDb, "UPDATE Users SET SecurityVersion = SecurityVersion + 1 WHERE Id = $id", ("$id", Id(userId)));

        Assert.Equal(HttpStatusCode.Unauthorized, (await primeira.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await segunda.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await outro.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task B7_sessao_expirada_no_servidor_responde_401()
    {
        var userId = Guid.NewGuid();
        var cliente = await _factory.ClienteAutenticadoAsync(userId, "Solicitante");

        _factory.ExecutarSql(_factory.BffDb, "UPDATE AuthSessions SET ExpiresAt = '2000-01-01 00:00:00' WHERE UserId = $id", ("$id", Id(userId)));

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task B8_papel_removido_na_fonte_vale_na_requisicao_seguinte_sem_novo_login()
    {
        var userId = Guid.NewGuid();
        var cliente = await _factory.ClienteAutenticadoAsync(userId, "Tecnico");
        // Sem equipe a fila responde 400 de regra; o que importa é passar do [Authorize(Roles)].
        Assert.NotEqual(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/v1/equipes/fila?page=1&pageSize=10")).StatusCode);

        _factory.ExecutarSql(_factory.BffDb, "DELETE FROM UsuarioPapeis WHERE UserId = $id", ("$id", Id(userId)));

        var me = await ChamadosHttp.JsonAsync(await cliente.GetAsync("/api/me"));
        Assert.Empty(me.GetProperty("papeis").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/v1/equipes/fila?page=1&pageSize=10")).StatusCode);
    }

    [Fact]
    public async Task B8_dev_login_recusa_papel_desconhecido()
    {
        var cliente = _factory.CreateClient();

        var desconhecido = await cliente.PostAsJsonAsync("/dev/login", new { userId = Guid.NewGuid(), roles = new[] { "Admin" } });

        Assert.Equal(HttpStatusCode.BadRequest, desconhecido.StatusCode);
    }

    [Fact]
    public async Task B6_outra_instancia_sobre_o_mesmo_banco_aceita_o_cookie_emitido_pela_primeira()
    {
        var (cliente, cookies) = _factory.ClienteComCookies();
        (await cliente.PostAsJsonAsync("/dev/login", new { userId = Guid.NewGuid(), roles = new[] { "Solicitante" } }))
            .EnsureSuccessStatusCode();

        using var segunda = new ApiFactory(_factory.Diretorio);
        var clienteDaSegunda = segunda.CreateDefaultClient(new Microsoft.AspNetCore.Mvc.Testing.Handlers.CookieContainerHandler(cookies));

        Assert.Equal(HttpStatusCode.OK, (await clienteDaSegunda.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Expurgo_remove_so_sessoes_encerradas_ha_mais_que_a_retencao()
    {
        var antiga = Guid.NewGuid();
        var revogada = Guid.NewGuid();
        await _factory.ClienteAutenticadoAsync(antiga, "Solicitante");
        var clienteRevogado = await _factory.ClienteAutenticadoAsync(revogada, "Solicitante");
        (await clienteRevogado.PostAsync("/auth/logout", null)).EnsureSuccessStatusCode();

        // Ainda dentro da retenção de 30 dias: nada sai.
        Assert.Equal(0, await ExpurgarAsync());

        _factory.Relogio.Avancar(TimeSpan.FromDays(31));
        var recente = Guid.NewGuid();
        var clienteRecente = await _factory.ClienteAutenticadoAsync(recente, "Solicitante");

        Assert.Equal(2, await ExpurgarAsync());
        Assert.Equal(1, _factory.ContarLinhas(_factory.BffDb, "AuthSessions"));
        Assert.Equal(HttpStatusCode.OK, (await clienteRecente.GetAsync("/api/me")).StatusCode);
    }

    private async Task<int> ExpurgarAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Api.Bff.SessoesDeAutenticacao>().ExpurgarAsync(CancellationToken.None);
    }
}
