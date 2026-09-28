using System.Net;
using Api.Bff;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SharedKernel;

namespace Api.IntegrationTests;

/// <summary>
/// Api em Production: sem /dev/login e sem EnsureCreated. O factory cria o schema e registra um
/// endpoint de login só de teste, para inspecionar o Set-Cookie (B3 de adaptacao-bff-angular.md).
/// </summary>
public sealed class ApiFactoryDeProducao : ApiFactory
{
    protected override string Ambiente => "Production";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddTransient<IStartupFilter, LoginDeTeste>());
    }

    /// <summary>Schema criado antes de o host subir: a Data Protection lê as chaves já na partida.</summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();
        using (var scope = host.Services.CreateScope())
        {
            foreach (var initializer in scope.ServiceProvider.GetServices<IDatabaseInitializer>())
            {
                initializer.EnsureCreatedAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        host.Start();
        return host;
    }

    /// <summary>Como a requisição chega depois do TLS do proxy reverso (D4).</summary>
    public HttpClient ClienteHttps() =>
        CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private sealed class LoginDeTeste : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Map("/teste/signin", ramo => ramo.Run(async context =>
            {
                var cadastro = context.RequestServices.GetRequiredService<CadastroDeUsuarios>();
                var sessoes = context.RequestServices.GetRequiredService<SessoesDeAutenticacao>();
                var userId = Guid.NewGuid();
                await cadastro.GarantirUsuarioDeDesenvolvimentoAsync(userId, ["Solicitante"], nome: null, CancellationToken.None);
                var sessao = await sessoes.CriarAsync(userId, CancellationToken.None);
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, SessoesDeAutenticacao.CriarPrincipal(sessao));
                context.Response.StatusCode = StatusCodes.Status204NoContent;
            }));
            next(app);
        };
    }
}

public sealed class CookieDeProducaoTests : IDisposable
{
    private readonly ApiFactoryDeProducao _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task B3_cookie_de_sessao_fora_de_Development_e_Host_Secure_HttpOnly_e_Lax()
    {
        var resposta = await _factory.ClienteHttps().GetAsync("/teste/signin");

        var sessao = Assert.Single(resposta.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-Sessao=", StringComparison.Ordinal))
            .ToLowerInvariant();
        Assert.Contains("secure", sessao);
        Assert.Contains("httponly", sessao);
        Assert.Contains("samesite=lax", sessao);
        Assert.Contains("path=/", sessao);
        Assert.DoesNotContain("domain=", sessao);
    }

    [Fact]
    public async Task B1_cookies_de_csrf_fora_de_Development_sao_Secure_e_o_do_antiforgery_e_Host()
    {
        var resposta = await _factory.ClienteHttps().GetAsync("/auth/csrf");

        var cookies = resposta.Headers.GetValues("Set-Cookie").Select(c => c.ToLowerInvariant()).ToArray();
        var antiforgery = Assert.Single(cookies, c => c.StartsWith("__host-antiforgery=", StringComparison.Ordinal));
        Assert.Contains("secure", antiforgery);
        Assert.Contains("httponly", antiforgery);
        var xsrf = Assert.Single(cookies, c => c.StartsWith("xsrf-token=", StringComparison.Ordinal));
        Assert.Contains("secure", xsrf);
        Assert.DoesNotContain("httponly", xsrf);
    }

    [Fact]
    public async Task B10_pagina_de_login_fora_de_Development_nao_tem_personas_e_dev_login_nao_existe()
    {
        var cliente = _factory.ClienteHttps();

        var html = await (await cliente.GetAsync("/auth/login")).Content.ReadAsStringAsync();

        Assert.Contains("name=\"__RequestVerificationToken\"", html);
        Assert.DoesNotContain("Entrar como (desenvolvimento)", html);
        Assert.DoesNotContain("dev-personas", html);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.PostAsync("/dev/login", null)).StatusCode);
    }
}
