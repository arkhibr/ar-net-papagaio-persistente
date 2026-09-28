namespace Api.Bff;

/// <summary>
/// Comandos de administração do provedor local (adaptacao-bff-angular.md, B10), no próprio
/// executável da Api, sem subir o servidor:
///
///   dotnet run --project src/Api -- criar-usuario &lt;login&gt; &lt;Papel&gt;[,&lt;Papel&gt;...] ["Nome de exibição"]
///   dotnet run --project src/Api -- redefinir-senha &lt;login&gt;
///   dotnet run --project src/Api -- definir-nome &lt;login&gt; "Nome de exibição"
///
/// A senha gerada é impressa uma única vez no console e nunca vai para log.
/// </summary>
internal static class ComandosDeAdministracao
{
    public static bool Reconhece(string[] args) =>
        args is ["criar-usuario", ..] or ["redefinir-senha", ..] or ["definir-nome", ..];

    public static async Task<int> ExecutarAsync(IServiceProvider services, string[] args)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        // Sem migrations no repositório (como os módulos): o comando garante o schema do BFF.
        await provider.GetRequiredService<BffDbContext>().Database.EnsureCreatedAsync();
        var cadastro = provider.GetRequiredService<CadastroDeUsuarios>();

        try
        {
            switch (args)
            {
                case ["criar-usuario", var login, var papeis, .. var resto] when resto.Length <= 1:
                {
                    var lista = papeis.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var nome = resto.Length == 1 ? resto[0] : null;
                    var (userId, senha) = await cadastro.CriarUsuarioLocalAsync(login, lista, nome, CancellationToken.None);
                    Console.WriteLine($"Usuário criado: {CredencialLocal.Normalizar(login)} ({userId})");
                    Console.WriteLine($"Senha (exibida só agora): {senha}");
                    return 0;
                }

                case ["redefinir-senha", var login]:
                {
                    var senha = await cadastro.RedefinirSenhaAsync(login, CancellationToken.None);
                    Console.WriteLine($"Senha redefinida para {CredencialLocal.Normalizar(login)}. Sessões abertas foram derrubadas.");
                    Console.WriteLine($"Senha nova (exibida só agora): {senha}");
                    return 0;
                }

                case ["definir-nome", var login, var nome]:
                    await cadastro.DefinirNomeAsync(login, nome, CancellationToken.None);
                    Console.WriteLine($"Nome de {CredencialLocal.Normalizar(login)} definido.");
                    return 0;

                default:
                    Console.Error.WriteLine(
                        "Uso: criar-usuario <login> <Papel>[,<Papel>...] [\"Nome\"] | redefinir-senha <login> | definir-nome <login> \"Nome\"");
                    return 2;
            }
        }
        catch (Exception erro) when (erro is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine(erro.Message);
            return 1;
        }
    }
}
