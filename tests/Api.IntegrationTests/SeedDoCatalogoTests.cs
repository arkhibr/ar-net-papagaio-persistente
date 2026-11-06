using System.Net;

namespace Api.IntegrationTests;

/// <summary>scripts/seed/catalogo.sql continua compatível com o schema real e pode rodar de novo.</summary>
public sealed class SeedDoCatalogoTests : IDisposable
{
    private static readonly Guid TecnicoInfra = Guid.Parse("A0000000-0000-0000-0000-000000000011");
    private static readonly Guid CategoriaLegado = Guid.Parse("C0000000-0000-0000-0000-000000000007");

    private readonly ApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static string Script()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "scripts", "seed", "catalogo.sql")))
        {
            diretorio = diretorio.Parent;
        }

        return File.ReadAllText(Path.Combine(diretorio!.FullName, "scripts", "seed", "catalogo.sql"));
    }

    [Fact]
    public async Task Seed_roda_duas_vezes_sem_duplicar_e_os_dados_funcionam_pela_api()
    {
        await Cenario.CriarAsync(_factory);

        _factory.ExecutarSql(_factory.CatalogoDb, Script());
        _factory.ExecutarSql(_factory.CatalogoDb, Script());

        Assert.Equal(3, _factory.ContarLinhas(_factory.CatalogoDb, "Equipes", "Id LIKE 'E0000000-%'"));
        Assert.Equal(7, _factory.ContarLinhas(_factory.CatalogoDb, "CategoriasDeServico", "Id LIKE 'C0000000-%'"));
        Assert.Equal(27, _factory.ContarLinhas(_factory.CatalogoDb, "SlasDeCategoria", "CategoriaId LIKE 'C0000000-%'"));
        Assert.Equal(6, _factory.ContarLinhas(_factory.CatalogoDb, "MembrosDeEquipe", "TecnicoId LIKE 'A0000000-%'"));

        var tecnico = await _factory.ClienteAutenticadoAsync(TecnicoInfra, "Tecnico");
        Assert.Equal(HttpStatusCode.OK, (await tecnico.GetAsync("/api/v1/equipes/fila?page=1&pageSize=10")).StatusCode);

        var solicitante = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        var critico = await solicitante.SendAsync(ChamadosHttp.Post(
            "/api/v1/chamados", new { categoriaId = CategoriaLegado, prioridade = 3 }, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.BadRequest, critico.StatusCode);
        await ChamadosHttp.AbrirAsync(solicitante, CategoriaLegado, prioridade: 1);
    }
}
