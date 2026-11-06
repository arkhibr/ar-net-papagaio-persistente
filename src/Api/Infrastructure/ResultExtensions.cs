using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.Infrastructure;

/// <summary>
/// Result/Result&lt;T&gt; → resposta HTTP. Falha de negócio → 400 regra-de-negocio-violada;
/// NotFound → 404, com o mesmo corpo de um acesso negado a recurso oculto (HttpErrors.NotFound).
/// </summary>
public static class ResultExtensions
{
    public static IActionResult ToActionResult(this Result result) =>
        result.IsSuccess ? new NoContentResult() : Falha(result.ErrorKind, result.Error!);

    public static IActionResult ToActionResult<T>(this Result<T> result, Func<T, IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess is not null ? onSuccess(result.Value!) : new OkObjectResult(result.Value);
        }

        return Falha(result.ErrorKind, result.Error!);
    }

    private static ProblemResult Falha(ErrorKind tipo, string mensagem) => new(tipo == ErrorKind.NotFound
        ? HttpErrors.NotFound()
        : HttpErrors.BusinessRule(mensagem));
}
