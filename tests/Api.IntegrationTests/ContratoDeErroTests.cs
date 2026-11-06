using System.Net;
using System.Text.Json;

namespace Api.IntegrationTests;

/// <summary>Achados C3 (validadores), A7 e M7 (formato errors[]) e M3 (404 idêntico) de achados.md.</summary>
public sealed class ContratoDeErroTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private Cenario _cenario = null!;

    public async Task InitializeAsync() => _cenario = await Cenario.CriarAsync(_factory);

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private static void AssertErrorsEhArray(JsonElement corpo)
    {
        Assert.True(corpo.TryGetProperty("errors", out var errors), "corpo sem errors");
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        Assert.NotEmpty(errors.EnumerateArray());
        foreach (var erro in errors.EnumerateArray())
        {
            Assert.True(erro.TryGetProperty("pointer", out _));
            Assert.True(erro.TryGetProperty("codigo", out _));
            Assert.True(erro.TryGetProperty("mensagem", out _));
        }
    }

    [Fact]
    public async Task C3_prioridade_fora_do_enum_responde_400_de_validacao_com_pointer()
    {
        var cliente = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");

        var resposta = await cliente.SendAsync(ChamadosHttp.Post(
            "/api/v1/chamados", new { categoriaId = _cenario.CategoriaId, prioridade = 99 }, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await ChamadosHttp.JsonAsync(resposta);
        AssertErrorsEhArray(corpo);
        Assert.Contains(corpo.GetProperty("errors").EnumerateArray(), e => e.GetProperty("pointer").GetString() == "prioridade");
    }

    [Fact]
    public async Task A7_corpo_malformado_responde_400_com_errors_em_array()
    {
        var cliente = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");

        var resposta = await cliente.SendAsync(ChamadosHttp.Post(
            "/api/v1/chamados", new { categoriaId = "abc", prioridade = 1 }, Guid.NewGuid().ToString()));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        AssertErrorsEhArray(await ChamadosHttp.JsonAsync(resposta));
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task M7_idempotency_key_ausente_responde_400_com_errors_em_array()
    {
        var cliente = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");

        var resposta = await cliente.SendAsync(ChamadosHttp.Post(
            "/api/v1/chamados", new { categoriaId = _cenario.CategoriaId, prioridade = 1 }));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        AssertErrorsEhArray(await ChamadosHttp.JsonAsync(resposta));
    }

    [Fact]
    public async Task M3_chamado_inexistente_e_chamado_sem_acesso_tem_a_mesma_resposta_404()
    {
        var dono = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");
        var chamadoAlheio = await ChamadosHttp.AbrirAsync(dono, _cenario.CategoriaId);
        var intruso = await _factory.ClienteAutenticadoAsync(Guid.NewGuid(), "Solicitante");

        var semAcesso = await intruso.GetAsync($"/api/v1/chamados/{chamadoAlheio}");
        var inexistente = await intruso.GetAsync($"/api/v1/chamados/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, semAcesso.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inexistente.StatusCode);

        var a = await ChamadosHttp.JsonAsync(semAcesso);
        var b = await ChamadosHttp.JsonAsync(inexistente);
        foreach (var campo in new[] { "type", "title", "status", "detail" })
        {
            Assert.Equal(a.GetProperty(campo).ToString(), b.GetProperty(campo).ToString());
        }
    }
}
