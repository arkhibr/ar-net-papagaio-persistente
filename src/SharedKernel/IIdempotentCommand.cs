namespace SharedKernel;

/// <summary>
/// Marca um Command cuja transição de estado não pode acontecer duas vezes por reenvio
/// acidental (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md). A chave
/// normalmente chega via header HTTP `Idempotency-Key`; Commands disparados por job geram a
/// própria chave.
/// </summary>
public interface IIdempotentCommand
{
    string IdempotencyKey { get; }
}
