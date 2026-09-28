using Api.Bff;
using Api.Infrastructure;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Asp.Versioning;
using Catalogo;
using Chamados;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.WebEncoders;
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

// --- BFF (arquitetura/17, Topologia A; adaptacao-bff-angular.md) --------------------------------
// Autenticação por cookie com sessão server-side (AuthSession/SecurityVersion), papéis resolvidos
// no servidor, CSRF, Data Protection persistida e login local provisório: ver Bff/.
builder.Services.AddBff(builder.Configuration, builder.Environment);

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

// Com views: a página de login do backend é uma view Razor (Views/Auth/Login.cshtml; B10). O
// encoder das views mantém acentos legíveis em vez de &#x..; (só afeta HTML, não o JSON).
builder.Services.Configure<WebEncoderOptions>(options =>
    options.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
builder.Services.AddControllersWithViews(options =>
    {
        // CSRF em toda action MVC mutável (POST/PUT/PATCH/DELETE), com 403 próprio no lugar do
        // 400 padrão (arquitetura/17; M1 de achados.md; adaptacao-bff-angular.md, B1).
        options.Filters.Add(new ValidarCsrfFilter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // 400 automático do [ApiController] (JSON malformado, tipo errado) no mesmo formato
        // errors[] de todo 400 (arquitetura/06; A7 de achados.md).
        options.InvalidModelStateResponseFactory = context =>
            new ProblemResult(HttpErrors.Validation(context.ModelState));
    });

// GlobalExceptionHandler (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md):
// IExceptionHandler nativo + AddProblemDetails() para o corpo RFC 9457 consistente.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.Configure<IdempotencyOptions>(builder.Configuration.GetSection(IdempotencyOptions.SectionName));

// Correlação (arquitetura/15, "Correlação nativa"): o scope que o ASP.NET abre por requisição
// (TraceId/RequestId) passa a aparecer em todo log da requisição (M9 de achados.md).
builder.Logging.AddSimpleConsole(options => options.IncludeScopes = true);

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

// criar-usuario / redefinir-senha (B10): executa e termina, sem subir o servidor.
if (ComandosDeAdministracao.Reconhece(args))
{
    return await ComandosDeAdministracao.ExecutarAsync(app.Services, args);
}

// Primeiro do pipeline: o resto enxerga o esquema e o IP originais do cliente (D4).
app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // Fora de Development o cookie de sessão é __Host- e Secure (B3). O SPA é servido por um
    // proxy reverso na frente (decisão D4), que termina o TLS e repassa /api e /auth.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.MapHealthChecks("/health");

// --- Ferramentas de desenvolvimento/teste local — nunca mapeadas fora de Development ----------
// Schema dos SQLite por EnsureCreated (nenhuma migration foi gerada ainda, inclusive a do
// BffDbContext) e o atalho /dev/login da página de login do backend. Guardado atrás de IsDevelopment() em dois níveis: a rota só é
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

    // POST /dev/login — atalho de desenvolvimento (decisão D5), usado pela seção de personas da
    // página de login do backend (B10), por testes-manuais.md e pelo ApiFactory. Cria User e
    // AuthSession de verdade e grava os papéis na fonte do servidor, nunca no cookie (B7/B8).
    // Isento de CSRF: é minimal API (fora do filtro MVC), cria a própria sessão e só existe aqui.
    // IsSystemActor nunca vem de login HTTP (B8).
    app.MapPost("/dev/login", async (
        DevLoginRequest request,
        HttpContext httpContext,
        CadastroDeUsuarios cadastro,
        SessoesDeAutenticacao sessoes,
        CancellationToken cancellationToken) =>
    {
        var papeis = request.Roles ?? [];
        try
        {
            await cadastro.GarantirUsuarioDeDesenvolvimentoAsync(request.UserId, papeis, request.Nome, cancellationToken);
        }
        catch (ArgumentException erro)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, detail: erro.Message);
        }

        var sessao = await sessoes.CriarAsync(request.UserId, cancellationToken);
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, SessoesDeAutenticacao.CriarPrincipal(sessao));

        return Results.Ok(new { request.UserId, Roles = papeis });
    });
}

await app.RunAsync();
return 0;

/// <summary>
/// Corpo de POST /dev/login. Roles usa os nomes de papel esperados por ICurrentUser.IsInRole
/// nesta solution: "Solicitante", "Tecnico", "Supervisor" (ver testes-manuais.md). Nome
/// opcional define o nome de exibição do usuário.
/// </summary>
internal sealed record DevLoginRequest(Guid UserId, string[]? Roles, string? Nome = null);

// Necessário para Api.IntegrationTests (WebApplicationFactory<Program>, próxima etapa do
// pipeline, construcao-de-testes) enxergar a classe Program gerada pelo top-level statement.
public partial class Program;
