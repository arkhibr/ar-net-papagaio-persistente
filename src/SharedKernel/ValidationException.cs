namespace SharedKernel;

/// <summary>
/// Um erro de validação sintática de um campo específico (arquitetura/16-validacao-sintatica-vs-invariante.md).
/// PropertyName é o nome do campo do Command/Query violado (equivalente ao "pointer" do corpo
/// RFC 9457 descrito em arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md); a
/// serialização HTTP em si (o Problem Details completo) é responsabilidade do
/// GlobalExceptionHandler na Api, não deste tipo.
/// </summary>
public sealed record ValidationError(string PropertyName, string ErrorMessage);

/// <summary>
/// Lançada pelo ValidationBehavior quando um ou mais IValidator&lt;TMessage&gt; (FluentValidation)
/// encontram falha sintática (arquitetura/16-validacao-sintatica-vs-invariante.md). Carrega
/// todos os erros agregados de uma vez (nunca só o primeiro) — arquitetura/06 exige que
/// ValidationException produza um `errors[]` com um item por regra de campo violada.
/// Traduzida para HTTP 400 pelo GlobalExceptionHandler.
/// </summary>
public sealed class ValidationException : Exception
{
    public IReadOnlyCollection<ValidationError> Errors { get; }

    public ValidationException(IReadOnlyCollection<ValidationError> errors)
        : base("Um ou mais campos são inválidos.")
    {
        Errors = errors;
    }
}
