using Api.Infrastructure;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Api.Bff;

/// <summary>
/// Token antiforgery legível pelo XSRF nativo do HttpClient do Angular (arquitetura/17, §CSRF;
/// M1 de achados.md; adaptacao-bff-angular.md, B1). O token é vinculado à identidade: é
/// reemitido em GET /auth/csrf e GET /api/me, que o Angular chama no bootstrap depois de todo
/// login.
/// </summary>
internal static class XsrfCookie
{
    public const string Nome = "XSRF-TOKEN";
    public const string Header = "X-XSRF-TOKEN";

    public static void Emitir(HttpContext httpContext)
    {
        var antiforgery = httpContext.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        var ambiente = httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();

        httpContext.Response.Cookies.Append(Nome, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Path = "/",
            SameSite = SameSiteMode.Strict,
            Secure = !ambiente.IsDevelopment() || httpContext.Request.IsHttps,
        });
    }
}

/// <summary>
/// Valida o antiforgery em toda action MVC mutável (POST/PUT/PATCH/DELETE; B1). Faz o que o
/// AutoValidateAntiforgeryTokenAttribute faria sem exigir os serviços de views do MVC, e responde
/// o 403 próprio de CSRF que arquitetura/17 pede, distinto do 403 de autorização (o filtro
/// padrão responde 400). Roda antes da action: nenhum Command é enviado e nenhuma
/// Idempotency-Key é reservada. Aceita o token no header X-XSRF-TOKEN (Angular) ou no campo de
/// formulário (página de login). Uma action pode se isentar com [IgnoreAntiforgeryToken].
/// </summary>
internal sealed class ValidarCsrfFilter : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var metodo = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo) || HttpMethods.IsOptions(metodo) || HttpMethods.IsTrace(metodo))
        {
            return;
        }

        var politica = context.ActionDescriptor.EndpointMetadata.OfType<IAntiforgeryMetadata>().LastOrDefault();
        if (politica is { RequiresValidation: false })
        {
            return;
        }

        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        if (!await antiforgery.IsRequestValidAsync(context.HttpContext))
        {
            context.Result = new ProblemResult(HttpErrors.CsrfInvalid());
        }
    }
}
