using System.Net;
using System.Text.RegularExpressions;
using Api.Bff;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests;

/// <summary>Apoio aos testes do BFF (adaptacao-bff-angular.md): login local, form e cadastro.</summary>
public static partial class BffHttp
{
    public const string MensagemDeFalha = "Login ou senha inválidos.";

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex CampoAntiforgery();

    /// <summary>Cria o usuário pelo mesmo serviço do comando criar-usuario.</summary>
    public static async Task<(Guid UserId, string Senha)> CriarUsuarioLocalAsync(
        ApiFactory factory, string login, params string[] papeis)
    {
        using var scope = factory.Services.CreateScope();
        var cadastro = scope.ServiceProvider.GetRequiredService<CadastroDeUsuarios>();
        return await cadastro.CriarUsuarioLocalAsync(login, papeis, nome: null, CancellationToken.None);
    }

    public static async Task<string> RedefinirSenhaAsync(ApiFactory factory, string login)
    {
        using var scope = factory.Services.CreateScope();
        var cadastro = scope.ServiceProvider.GetRequiredService<CadastroDeUsuarios>();
        return await cadastro.RedefinirSenhaAsync(login, CancellationToken.None);
    }

    /// <summary>GET /auth/login e extrai o token antiforgery de formulário do HTML.</summary>
    public static async Task<string> TokenDoFormularioAsync(HttpClient cliente, string returnUrl = "/")
    {
        var html = await (await cliente.GetAsync($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}")).Content.ReadAsStringAsync();
        return WebUtility.HtmlDecode(CampoAntiforgery().Match(html).Groups[1].Value);
    }

    public static async Task<HttpResponseMessage> PostarLoginAsync(
        HttpClient cliente, string login, string senha, string returnUrl = "/", bool comToken = true)
    {
        var campos = new Dictionary<string, string> { ["login"] = login, ["senha"] = senha, ["returnUrl"] = returnUrl };
        if (comToken)
        {
            campos["__RequestVerificationToken"] = await TokenDoFormularioAsync(cliente, returnUrl);
        }

        return await cliente.PostAsync("/auth/login", new FormUrlEncodedContent(campos));
    }
}
