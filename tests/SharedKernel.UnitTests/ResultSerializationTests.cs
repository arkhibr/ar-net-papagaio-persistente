using System.Text.Json;
using SharedKernel;
using Xunit;

namespace SharedKernel.UnitTests;

/// <summary>
/// Round-trip de serialização real (não só construir em memória) de Result&lt;T&gt;, para não
/// reintroduzir silenciosamente um construtor privado sem [JsonConstructor]
/// (13-estrategia-de-testes.md, "Serialização de Result&lt;T&gt;"). Relevante em especial porque o
/// IdempotencyBehavior serializa a resposta de sucesso para devolver em reenvios futuros
/// sob a mesma Idempotency-Key (arquitetura/06, arquitetura/25).
/// </summary>
public class ResultSerializationTests
{
    [Fact]
    public void Result_de_sucesso_deve_sobreviver_a_um_round_trip_de_serializacao()
    {
        var original = Result<Guid>.Success(Guid.NewGuid());

        var json = JsonSerializer.Serialize(original);
        var deserializado = JsonSerializer.Deserialize<Result<Guid>>(json);

        Assert.NotNull(deserializado);
        Assert.True(deserializado!.IsSuccess);
        Assert.False(deserializado.IsFailure);
        Assert.Equal(original.Value, deserializado.Value);
        Assert.Null(deserializado.Error);
    }

    [Fact]
    public void Result_de_falha_deve_sobreviver_a_um_round_trip_de_serializacao()
    {
        var original = Result<Guid>.Failure("Não foi possível executar a operação.");

        var json = JsonSerializer.Serialize(original);
        var deserializado = JsonSerializer.Deserialize<Result<Guid>>(json);

        Assert.NotNull(deserializado);
        Assert.False(deserializado!.IsSuccess);
        Assert.True(deserializado.IsFailure);
        Assert.Equal(original.Error, deserializado.Error);
        Assert.Equal(default, deserializado.Value);
    }

    [Fact]
    public void Result_sem_valor_de_sucesso_deve_sobreviver_a_um_round_trip_de_serializacao()
    {
        var original = Result.Success();

        var json = JsonSerializer.Serialize(original);
        var deserializado = JsonSerializer.Deserialize<Result>(json);

        Assert.NotNull(deserializado);
        Assert.True(deserializado!.IsSuccess);
    }
}
