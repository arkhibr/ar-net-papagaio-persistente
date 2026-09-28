using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests;

public static class ChamadosHttp
{
    public static HttpRequestMessage Post(string url, object? corpo, string? idempotencyKey = null, string? ifMatch = null)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = corpo is null ? null : JsonContent.Create(corpo),
        };

        if (idempotencyKey is not null)
        {
            requisicao.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        if (ifMatch is not null)
        {
            requisicao.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return requisicao;
    }

    public static async Task<Guid> AbrirAsync(HttpClient cliente, Guid categoriaId, int prioridade = 1, string? chave = null)
    {
        var resposta = await cliente.SendAsync(
            Post("/api/v1/chamados", new { categoriaId, prioridade }, chave ?? Guid.NewGuid().ToString()));
        var corpo = await resposta.Content.ReadAsStringAsync();
        if ((int)resposta.StatusCode != 201)
        {
            throw new InvalidOperationException($"Abrir falhou: {(int)resposta.StatusCode} {corpo}");
        }

        return JsonDocument.Parse(corpo).RootElement.GetProperty("id").GetGuid();
    }

    public static async Task<JsonElement> JsonAsync(HttpResponseMessage resposta) =>
        JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
}
