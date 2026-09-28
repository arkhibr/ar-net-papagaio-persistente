using System.Text.Json.Serialization;

namespace SharedKernel;

/// <summary>
/// Interface-base usada pelo UnitOfWorkBehavior para inspecionar sucesso/falha sem
/// conhecer o T concreto (ver arquitetura/25-transacao-e-unit-of-work.md).
/// </summary>
public interface IResult
{
    bool IsSuccess { get; }
    bool IsFailure { get; }
}

/// <summary>
/// Natureza de uma falha determinística. NotFound existe para a Api responder 404 sem
/// depender do texto da mensagem (arquitetura/06, arquitetura/14 — Result.NotFound()).
/// </summary>
public enum ErrorKind
{
    None = 0,
    BusinessRule = 1,
    NotFound = 2,
}

/// <summary>
/// Resultado de uma operação sem valor de retorno relevante além de sucesso/falha.
/// Falha de negócio determinística (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md):
/// nunca usado para falha transitória de infraestrutura, que continua sendo exceção.
/// </summary>
public sealed class Result : IResult
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }
    public ErrorKind ErrorKind { get; }

    [JsonConstructor]
    private Result(bool isSuccess, string? error, ErrorKind errorKind)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorKind = errorKind;
    }

    public static Result Success() => new(true, null, ErrorKind.None);

    public static Result Failure(string error) => new(false, error, ErrorKind.BusinessRule);

    public static Result NotFound(string error) => new(false, error, ErrorKind.NotFound);
}

/// <summary>
/// Resultado de uma operação com valor de retorno em caso de sucesso. Construtor privado
/// com [JsonConstructor]: serializar/desserializar via System.Text.Json precisa continuar
/// funcionando (é o próprio DTO devolvido pelo IdempotencyBehavior em reenvios), por isso
/// há teste dedicado de round-trip de serialização (ver 13-estrategia-de-testes.md).
/// </summary>
public sealed class Result<T> : IResult
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public string? Error { get; }
    public ErrorKind ErrorKind { get; }

    [JsonConstructor]
    private Result(bool isSuccess, T? value, string? error, ErrorKind errorKind)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        ErrorKind = errorKind;
    }

    public static Result<T> Success(T value) => new(true, value, null, ErrorKind.None);

    public static Result<T> Failure(string error) => new(false, default, error, ErrorKind.BusinessRule);

    public static Result<T> NotFound(string error) => new(false, default, error, ErrorKind.NotFound);
}
