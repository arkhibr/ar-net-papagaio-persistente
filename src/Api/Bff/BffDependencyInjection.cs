using System.Threading.RateLimiting;
using Api.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Api.Bff;

/// <summary>
/// Papel de BFF da Api na Topologia A (arquitetura/17): termina a autenticação, guarda a sessão
/// no servidor, expõe a sessão por cookie e valida CSRF (adaptacao-bff-angular.md, Fase 1 e 2).
/// </summary>
internal static class BffDependencyInjection
{
    public const string PoliticaLoginLocal = "login-local";

    public static IServiceCollection AddBff(
        this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var desenvolvimento = environment.IsDevelopment();

        AdicionarOpcoes<BffOptions>(services, configuration, BffOptions.SectionName);
        AdicionarOpcoes<SessaoOptions>(services, configuration, SessaoOptions.SectionName);
        AdicionarOpcoes<LoginLocalOptions>(services, configuration, LoginLocalOptions.SectionName);

        // Proxy reverso na frente (D4): esquema e IP do cliente vêm de X-Forwarded-*.
        var proxy = configuration.GetSection(ProxyReversoOptions.SectionName).Get<ProxyReversoOptions>() ?? new();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var rede in proxy.RedesConfiaveis)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(rede));
            }
        });

        // Store técnico da borda (decisão D3 = banco relacional; B6/B7).
        services.AddDbContext<BffDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<IOptions<BffOptions>>().Value.ConnectionString));
        services.AddScoped<IDatabaseInitializer, BffDatabaseInitializer>();
        services.AddHealthChecks().AddDbContextCheck<BffDbContext>(name: "bff-db");

        // Chave que cifra o cookie num store compartilhado entre réplicas (arquitetura/10; B6).
        services.AddDataProtection()
            .PersistKeysToDbContext<BffDbContext>()
            .SetApplicationName("chamados");

        services.AddScoped<SessoesDeAutenticacao>();
        services.AddScoped<CadastroDeUsuarios>();
        services.AddScoped<LoginLocal>();
        services.AddScoped<DiretorioDeUsuarios>();
        services.AddSingleton<IPasswordHasher<CredencialLocal>, PasswordHasher<CredencialLocal>>();
        services.AddScoped<SessaoCookieEvents>();
        services.AddHostedService<ExpurgoDeSessoesService>();
        services.AddTransient<IClaimsTransformation, PapeisDoServidorTransformation>();

        // Cookie de sessão endurecido (arquitetura/11, "Tokens e o padrão BFF"; M2; B3). Lax, não
        // Strict: o retorno do IdP (B9) chega como navegação vinda de outro site.
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = desenvolvimento ? "Sessao" : "__Host-Sessao";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = desenvolvimento ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
                options.SlidingExpiration = true;
                options.EventsType = typeof(SessaoCookieEvents);
            });
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<SessaoOptions>>((cookie, sessao) => cookie.ExpireTimeSpan = sessao.Value.Duracao);
        services.AddAuthorization();

        // CSRF (arquitetura/11 e 17; M1; B1). O cookie do antiforgery é HttpOnly; o token legível
        // pelo Angular vai em XSRF-TOKEN (XsrfCookie).
        services.AddAntiforgery(options =>
        {
            options.HeaderName = XsrfCookie.Header;
            options.Cookie.Name = desenvolvimento ? "Antiforgery" : "__Host-Antiforgery";
            options.Cookie.SecurePolicy = desenvolvimento ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.HttpOnly = true;
            options.Cookie.Path = "/";
        });

        // Rate limiting do login local por IP, complementar ao bloqueio por login (B10).
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PoliticaLoginLocal, httpContext =>
            {
                var limite = httpContext.RequestServices.GetRequiredService<IOptions<LoginLocalOptions>>().Value;
                return RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limite.TentativasPorMinutoPorIp,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    });
            });
            options.OnRejected = (context, _) =>
                new ValueTask(ProblemResult.WriteAsync(context.HttpContext, HttpErrors.TooManyRequests()));
        });

        return services;
    }

    private static void AdicionarOpcoes<T>(IServiceCollection services, IConfiguration configuration, string secao)
        where T : class =>
        services.AddOptions<T>()
            .Bind(configuration.GetSection(secao))
            .ValidateDataAnnotations()
            .ValidateOnStart();
}
