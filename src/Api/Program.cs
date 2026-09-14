using System.Security.Claims;
using Api.Infrastructure;
using Asp.Versioning;
using Catalogo;
using Chamados;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using SharedKernel;
using SharedKernel.Messaging;

var builder = WebApplication.CreateBuilder(args);

// --- Composição raiz (arquitetura/01-estrutura-de-projetos-monolito-modular.md, "Como Api
// compõe o sistema"): SharedKernelPipeline primeiro (behaviors genéricos), depois cada módulo. ---
builder.Services
    .AddSharedKernelPipeline()
    .AddChamadosModule(builder.Configuration)
    .AddCatalogoModule(builder.Configuration);

// ISender único que os controllers injetam: CompositeSender (Api/Infrastructure/CompositeSender.cs)
// roteia para o IMediator keyed do módulo dono do tipo da mensagem — necessário porque cada
// módulo gera o próprio Mediator.Mediator (Mediator.SourceGenerator local a cada assembly), e
// só um "ISender sem chave" venceria por TryAdd se registrado ingenuamente (ver comentário da
// classe). Nunca resolver ISender/IMediator sem chave diretamente dos módulos a partir daqui.
builder.Services.AddScoped<Mediator.ISender, CompositeSender>();

// ICurrentUser: implementação real lendo HttpContext.User, registrada aqui (composição raiz),
// nunca em SharedKernel/módulo (arquitetura/11-autenticacao-e-sessao.md, "Domain nunca depende
// disto; só Application/Api").
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, Api.Infrastructure.HttpContextCurrentUser>();

// --- Autenticação/autorização: escopo mínimo desta rodada -------------------------------------
// PENDÊNCIA EXPLÍCITA (não é "feito", é scaffolding): arquitetura/11-autenticacao-e-sessao.md
// completo (login, emissão de cookie real a partir de usuário/senha, identity provider) NÃO
// está implementado nesta solution. O cookie scheme abaixo só existe para (a) os atributos
// [Authorize]/[Authorize(Roles=...)] dos controllers terem um esquema de autenticação válido
// para redirecionar/rejeitar quando ausente, e (b) uma futura Api.IntegrationTests poder
// autenticar requisições de teste por fora (ex.: TestAuthHandler), sem depender de um fluxo de
// login que ainda não existe. Nenhum endpoint de login/registro é exposto aqui — isso é lacuna
// registrada para uma rodada futura dedicada a arquitetura/11, não inventada por conta própria.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Como não há endpoint de login ainda, uma requisição não autenticada tentando um
        // endpoint [Authorize] deve responder 401 (API), não redirecionar para uma tela de
        // login HTML que não existe — comportamento de API, não de aplicação com UI própria
        // hospedada neste processo.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

// CSRF (arquitetura/17-bff-angular-e-comunicacao.md): mecanismo XSRF nativo do HttpClient do
// Angular (cookie não-HttpOnly + header customizado correspondente). Cookie de sessão em si
// (Topologia A, mesmo processo) ainda depende do scaffolding de auth acima; o antiforgery já
// fica registrado desde já porque toda operação mutável (POST de Chamados) vai precisar dele
// assim que o login real existir — não é usado por nenhuma action ainda (nenhum [ValidateAntiForgeryToken]
// aplicado nesta rodada, já que não há sessão de verdade para gerar o token), registrado como
// scaffolding coerente com o restante do escopo mínimo desta seção.
builder.Services.AddAntiforgery(options => options.HeaderName = "X-XSRF-TOKEN");

// --- Versionamento (/api/v1, arquitetura/18-versionamento-de-api.md): segmento de URL, nunca
// header/media type. ---
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.ReportApiVersions = true;
        // Segmento de URL (/api/v1/...) explícito em todo controller desta rodada
        // ([Route("api/v{version:apiVersion}/...")]) — nunca header/media type
        // (arquitetura/18-versionamento-de-api.md). Declarar o reader evita a biblioteca
        // inspecionar query string/header a cada requisição (AV0015).
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddControllers();

// GlobalExceptionHandler (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md):
// IExceptionHandler nativo + AddProblemDetails() para o corpo RFC 9457 consistente.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Documento OpenAPI mínimo (arquitetura/18-versionamento-de-api.md: gerador nativo
// Microsoft.AspNetCore.OpenApi, não Swashbuckle). Um documento por versão (WithDocumentPerVersion,
// sugerido pelo analyzer AV0029/AV0030 do próprio Asp.Versioning.Mvc.ApiExplorer) exigiria mais
// um pacote de integração (Asp.Versioning + Microsoft.AspNetCore.OpenApi não trazem esse método
// por padrão) só para múltiplas versões — sem ganho nesta rodada, já que só existe "v1"
// publicada (arquitetura/18: nova versão só quando houver breaking change real). Documento único
// aceito como suficiente agora; revisar quando uma v2 existir de fato.
builder.Services.AddOpenApi();

// Health checks agregados: cada módulo já registrou a própria checagem dentro do próprio
// Add{Modulo}Module (AddDbContextCheck<T>, arquitetura/06 "cada camada registra a checagem que
// sabe fazer; a Api só agrega em /health"). Nenhuma chamada a AddHealthChecks() aqui além do
// mapeamento do endpoint: a Api nunca conhece o que cada módulo verifica.
var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

// --- Ferramentas de desenvolvimento/teste local — nunca mapeadas fora de Development ----------
// Sem isso, testes-manuais.md não teria como autenticar via curl (nenhum login real existe,
// ver PENDÊNCIA EXPLÍCITA em HttpContextCurrentUser.cs) nem como o SQLite ter schema (nenhuma
// migration foi gerada ainda). Guardado atrás de IsDevelopment() em dois níveis: a rota só é
// mapeada neste bloco (nem aparece no roteamento fora de Development) e o app nunca chama isto
// a partir de outro Environment.
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        foreach (var initializer in scope.ServiceProvider.GetServices<IDatabaseInitializer>())
        {
            await initializer.EnsureCreatedAsync(CancellationToken.None);
        }
    }

    // POST /dev/login — emite o cookie que HttpContextCurrentUser espera, sem passar por
    // identity provider nenhum (arquitetura/11 completo é pendência registrada, não implementada
    // aqui). Fora do /api/v{version}, fora de qualquer controller: não é superfície pública da
    // API real, é só instrumentação de teste local, visível como tal na própria rota.
    app.MapPost("/dev/login", async (DevLoginRequest request, HttpContext httpContext) =>
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, request.UserId.ToString()),
            new("sid", Guid.NewGuid().ToString()),
        };
        claims.AddRange((request.Roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));
        if (request.IsSystemActor)
        {
            claims.Add(new Claim("system_actor", "true"));
        }

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return Results.Ok(new { request.UserId, request.Roles, request.IsSystemActor });
    });

    app.MapPost("/dev/logout", async (HttpContext httpContext) =>
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Ok();
    });
}

app.Run();

/// <summary>
/// Corpo de POST /dev/login. Roles usa os nomes de papel esperados por ICurrentUser.IsInRole
/// nesta solution: "Solicitante", "Tecnico", "Supervisor" (ver testes-manuais.md).
/// </summary>
internal sealed record DevLoginRequest(Guid UserId, string[]? Roles, bool IsSystemActor = false);

// Necessário para Api.IntegrationTests (WebApplicationFactory<Program>, próxima etapa do
// pipeline, construcao-de-testes) enxergar a classe Program gerada pelo top-level statement.
public partial class Program;
