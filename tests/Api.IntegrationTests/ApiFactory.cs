using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.IntegrationTests;

/// <summary>
/// Sobe a Api real (Program) em Development, com bancos SQLite próprios por instância em
/// diretório temporário — nunca os .db de src/Api. Development é necessário porque o schema
/// vem de IDatabaseInitializer e a autenticação de teste vem de POST /dev/login
/// (ver Program.cs); nenhum dos dois existe fora de Development.
///
/// Dois factories sobre o mesmo diretório compartilham os bancos, inclusive as chaves de Data
/// Protection (B6 de adaptacao-bff-angular.md).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _diretorio;
    private readonly bool _donoDoDiretorio;
    private readonly Dictionary<string, string> _configuracao = new();

    public ApiFactory()
        : this(Path.Combine(Path.GetTempPath(), "api-it-" + Guid.NewGuid().ToString("N")), donoDoDiretorio: true)
    {
    }

    public ApiFactory(string diretorio, bool donoDoDiretorio = false)
    {
        _diretorio = diretorio;
        _donoDoDiretorio = donoDoDiretorio;
    }

    public string Diretorio => _diretorio;
    public string CatalogoDb => Path.Combine(_diretorio, "catalogo.db");
    public string ChamadosDb => Path.Combine(_diretorio, "chamados.db");
    public string BffDb => Path.Combine(_diretorio, "bff.db");

    /// <summary>Relógio controlado pelo teste (bloqueio do login local, expiração).</summary>
    public RelogioDeTeste Relogio { get; } = new();

    protected virtual string Ambiente => "Development";

    /// <summary>Sobrescreve uma chave de configuração antes de a Api subir.</summary>
    public ApiFactory ComConfiguracao(string chave, string valor)
    {
        _configuracao[chave] = valor;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_diretorio);
        builder.UseEnvironment(Ambiente);
        builder.UseSetting("Chamados:ConnectionString", $"Data Source={ChamadosDb}");
        builder.UseSetting("Catalogo:ConnectionString", $"Data Source={CatalogoDb}");
        builder.UseSetting("Bff:ConnectionString", $"Data Source={BffDb}");
        foreach (var (chave, valor) in _configuracao)
        {
            builder.UseSetting(chave, valor);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Relogio);
        });
    }

    /// <summary>Cliente com cookie próprio e anônimo; o CookieContainer fica acessível ao teste.</summary>
    public (HttpClient Cliente, CookieContainer Cookies) ClienteComCookies()
    {
        var cookies = new CookieContainer();
        var cliente = CreateDefaultClient(new CookieContainerHandler(cookies));
        return (cliente, cookies);
    }

    /// <summary>
    /// Cliente autenticado via /dev/login, com cookie próprio e o header X-XSRF-TOKEN já
    /// configurado (B1: toda action mutável exige CSRF).
    /// </summary>
    public async Task<HttpClient> ClienteAutenticadoAsync(Guid userId, params string[] papeis)
    {
        var (cliente, cookies) = ClienteComCookies();
        var resposta = await cliente.PostAsJsonAsync("/dev/login", new { userId, roles = papeis });
        resposta.EnsureSuccessStatusCode();
        await RenovarXsrfAsync(cliente, cookies);
        return cliente;
    }

    /// <summary>GET /auth/csrf e copia o cookie XSRF-TOKEN para o header, como o Angular faz.</summary>
    public static async Task RenovarXsrfAsync(HttpClient cliente, CookieContainer cookies)
    {
        (await cliente.GetAsync("/auth/csrf")).EnsureSuccessStatusCode();
        cliente.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        cliente.DefaultRequestHeaders.Add("X-XSRF-TOKEN", Xsrf(cookies));
    }

    public static string Xsrf(CookieContainer cookies) =>
        Uri.UnescapeDataString(cookies.GetCookies(new Uri("http://localhost"))["XSRF-TOKEN"]!.Value);

    public void ExecutarSql(string arquivo, string sql, params (string Nome, object Valor)[] parametros)
    {
        using var conexao = new SqliteConnection($"Data Source={arquivo}");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nome, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nome, valor);
        }

        comando.ExecuteNonQuery();
    }

    public long ContarLinhas(string arquivo, string tabela, string where = "1=1", params (string Nome, object Valor)[] parametros)
    {
        using var conexao = new SqliteConnection($"Data Source={arquivo}");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = $"SELECT COUNT(*) FROM {tabela} WHERE {where}";
        foreach (var (nome, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nome, valor);
        }

        return (long)comando.ExecuteScalar()!;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (!_donoDoDiretorio)
        {
            return;
        }

        try
        {
            Directory.Delete(_diretorio, recursive: true);
        }
        catch (IOException)
        {
            // Arquivo ainda preso por outro handle: diretório temporário, o SO limpa depois.
        }
    }
}

/// <summary>TimeProvider que o teste avança à mão.</summary>
public sealed class RelogioDeTeste : TimeProvider
{
    private DateTimeOffset _agora = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _agora;

    public void Avancar(TimeSpan intervalo) => _agora += intervalo;
}
