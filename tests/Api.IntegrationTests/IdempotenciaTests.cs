using System.Net;

namespace Api.IntegrationTests;

/// <summary>Achados C1 (escopo da chave) e A4 (corrida da mesma chave) de achados.md.</summary>
public sealed class IdempotenciaTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private Cenario _cenario = null!;

    public async Task InitializeAsync() => _cenario = await Cenario.CriarAsync(_factory);

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task C1_mesma_chave_de_outro_usuario_cria_o_proprio_chamado()
    {
        var chave = Guid.NewGuid().ToString();
        var clienteA = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        var clienteB = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");

        var idA = await ChamadosHttp.AbrirAsync(clienteA, _cenario.CategoriaId, chave: chave);
        var idB = await ChamadosHttp.AbrirAsync(clienteB, _cenario.CategoriaId, chave: chave);

        Assert.NotEqual(idA, idB);
        Assert.Equal(2, _factory.ContarLinhas(_factory.ChamadosDb, "Chamados"));
    }

    [Fact]
    public async Task C1_mesma_chave_com_payload_diferente_responde_422()
    {
        var chave = Guid.NewGuid().ToString();
        var cliente = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        await ChamadosHttp.AbrirAsync(cliente, _cenario.CategoriaId, prioridade: 1, chave: chave);

        var resposta = await cliente.SendAsync(ChamadosHttp.Post(
            "/api/v1/chamados", new { categoriaId = _cenario.CategoriaId, prioridade = 2 }, chave));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
    }

    [Fact]
    public async Task C1_mesma_chave_e_mesmo_payload_devolve_a_resposta_original()
    {
        var chave = Guid.NewGuid().ToString();
        var cliente = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");

        var primeiro = await ChamadosHttp.AbrirAsync(cliente, _cenario.CategoriaId, chave: chave);
        var segundo = await ChamadosHttp.AbrirAsync(cliente, _cenario.CategoriaId, chave: chave);

        Assert.Equal(primeiro, segundo);
        Assert.Equal(1, _factory.ContarLinhas(_factory.ChamadosDb, "Chamados"));
    }

    [Fact]
    public async Task A4_requisicoes_simultaneas_com_a_mesma_chave_nunca_respondem_500()
    {
        var chave = Guid.NewGuid().ToString();
        var cliente = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");

        var respostas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => cliente.SendAsync(
            ChamadosHttp.Post("/api/v1/chamados", new { categoriaId = _cenario.CategoriaId, prioridade = 1 }, chave))));

        var status = respostas.Select(r => (int)r.StatusCode).ToList();
        Assert.All(status, s => Assert.Contains(s, new[] { 201, 409 }));
        Assert.Equal(1, _factory.ContarLinhas(_factory.ChamadosDb, "Chamados"));
    }
}
