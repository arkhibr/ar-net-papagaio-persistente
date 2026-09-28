using System.Net;

namespace Api.IntegrationTests;

/// <summary>B10 de adaptacao-bff-angular.md: provedor local de credenciais com página de login no backend.</summary>
public sealed class LoginLocalTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task B10_login_valido_redireciona_ao_returnUrl_e_me_traz_os_papeis_de_UsuarioPapel()
    {
        var (userId, senha) = await BffHttp.CriarUsuarioLocalAsync(_factory, " Maria.Silva ", "Solicitante", "Tecnico");
        var (cliente, _) = _factory.ClienteComCookies();

        var resposta = await BffHttp.PostarLoginAsync(cliente, "MARIA.silva", senha, "/chamados/meus");

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/chamados/meus", resposta.Headers.Location?.OriginalString);
        var me = await ChamadosHttp.JsonAsync(await cliente.GetAsync("/api/me"));
        Assert.Equal(userId, me.GetProperty("userId").GetGuid());
        Assert.Equal(["Solicitante", "Tecnico"], me.GetProperty("papeis").EnumerateArray().Select(p => p.GetString()));
    }

    [Fact]
    public async Task B10_senha_errada_e_login_inexistente_produzem_a_mesma_resposta()
    {
        await BffHttp.CriarUsuarioLocalAsync(_factory, "joao", "Solicitante");
        var (cliente, _) = _factory.ClienteComCookies();

        var senhaErrada = await BffHttp.PostarLoginAsync(cliente, "joao", "errada");
        var inexistente = await BffHttp.PostarLoginAsync(cliente, "ninguem", "errada");

        Assert.Equal(HttpStatusCode.OK, senhaErrada.StatusCode);
        Assert.Equal(HttpStatusCode.OK, inexistente.StatusCode);
        Assert.Contains(BffHttp.MensagemDeFalha, await senhaErrada.Content.ReadAsStringAsync());
        Assert.Contains(BffHttp.MensagemDeFalha, await inexistente.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task B10_cinco_senhas_erradas_bloqueiam_por_15_minutos_mesmo_com_a_senha_certa()
    {
        var (_, senha) = await BffHttp.CriarUsuarioLocalAsync(_factory, "ana", "Solicitante");
        var (cliente, _) = _factory.ClienteComCookies();

        for (var i = 0; i < 5; i++)
        {
            await BffHttp.PostarLoginAsync(cliente, "ana", "errada");
        }

        var bloqueado = await BffHttp.PostarLoginAsync(cliente, "ana", senha);
        Assert.Equal(HttpStatusCode.OK, bloqueado.StatusCode);
        Assert.Contains(BffHttp.MensagemDeFalha, await bloqueado.Content.ReadAsStringAsync());

        _factory.Relogio.Avancar(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.Redirect, (await BffHttp.PostarLoginAsync(cliente, "ana", senha)).StatusCode);
    }

    [Fact]
    public async Task B10_post_de_login_sem_token_antiforgery_e_recusado()
    {
        var (_, senha) = await BffHttp.CriarUsuarioLocalAsync(_factory, "bia", "Solicitante");
        var (cliente, _) = _factory.ClienteComCookies();

        var resposta = await BffHttp.PostarLoginAsync(cliente, "bia", senha, comToken: false);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task B10_decima_primeira_tentativa_no_mesmo_minuto_responde_429()
    {
        var (cliente, _) = _factory.ClienteComCookies();
        var token = await BffHttp.TokenDoFormularioAsync(cliente);
        var campos = new Dictionary<string, string>
        {
            ["login"] = "x", ["senha"] = "y", ["returnUrl"] = "/", ["__RequestVerificationToken"] = token,
        };

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsync("/auth/login", new FormUrlEncodedContent(campos))).StatusCode);
        }

        var excedente = await cliente.PostAsync("/auth/login", new FormUrlEncodedContent(campos));
        Assert.Equal((HttpStatusCode)429, excedente.StatusCode);
        Assert.EndsWith("/muitas-tentativas", (await ChamadosHttp.JsonAsync(excedente)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task B10_redefinir_senha_derruba_sessao_ja_aberta()
    {
        var (_, senha) = await BffHttp.CriarUsuarioLocalAsync(_factory, "carla", "Solicitante");
        var (cliente, _) = _factory.ClienteComCookies();
        (await BffHttp.PostarLoginAsync(cliente, "carla", senha)).EnsureRedirect();
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/me")).StatusCode);

        var nova = await BffHttp.RedefinirSenhaAsync(_factory, "carla");

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await BffHttp.PostarLoginAsync(cliente, "carla", nova)).StatusCode);
    }

    [Fact]
    public async Task B4_returnUrl_externo_cai_para_a_raiz()
    {
        var (_, senha) = await BffHttp.CriarUsuarioLocalAsync(_factory, "davi", "Solicitante");
        var (cliente, _) = _factory.ClienteComCookies();

        var resposta = await BffHttp.PostarLoginAsync(cliente, "davi", senha, "https://algum.outro.local/");

        Assert.Equal("/", resposta.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task B10_pagina_de_login_com_sessao_aberta_redireciona_e_em_Development_mostra_personas()
    {
        var (cliente, _) = _factory.ClienteComCookies();
        var anonima = await (await cliente.GetAsync("/auth/login")).Content.ReadAsStringAsync();
        Assert.Contains("Entrar como (desenvolvimento)", anonima);

        var (_, senha) = await BffHttp.CriarUsuarioLocalAsync(_factory, "eva", "Solicitante");
        (await BffHttp.PostarLoginAsync(cliente, "eva", senha)).EnsureRedirect();

        var logado = await cliente.GetAsync("/auth/login?returnUrl=%2Fchamados%2Fnovo");
        Assert.Equal(HttpStatusCode.Redirect, logado.StatusCode);
        Assert.Equal("/chamados/novo", logado.Headers.Location?.OriginalString);
    }
}

internal static class RespostaExtensions
{
    public static void EnsureRedirect(this HttpResponseMessage resposta) =>
        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
}
