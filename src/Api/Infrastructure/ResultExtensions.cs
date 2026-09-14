using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.Infrastructure;

/// <summary>
/// Traduz Result/Result&lt;T&gt; (falha de negócio determinística devolvida normalmente pelo
/// handler, não exceção) em IActionResult. Fica fora do GlobalExceptionHandler de propósito
/// (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md, "Result.Failure vs.
/// exceção"): Result.Failure é caminho de retorno normal do pipeline, nunca uma exceção
/// capturada. Controllers reaproveitam este extension method para permanecerem finos (sem
/// reimplementar a tradução Result -> HTTP em cada action).
///
/// Corpo de erro segue o mesmo formato RFC 9457 do GlobalExceptionHandler (arquitetura/06,
/// "Lista de erros de negócio"): 400, com um item em errors[] e pointer nulo (a falha é do
/// agregado/regra de negócio, não de um campo específico do Command — validação sintática de
/// campo já teria sido barrada antes, pelo ValidationBehavior).
/// </summary>
public static class ResultExtensions
{
    private const string TipoFalhaDeNegocio = "https://arkhi.com.br/erros/regra-de-negocio-violada";

    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
        {
            return new NoContentResult();
        }

        return FalhaParaProblemDetails(result.Error!);
    }

    public static IActionResult ToActionResult<T>(this Result<T> result, Func<T, IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess is not null ? onSuccess(result.Value!) : new OkObjectResult(result.Value);
        }

        return FalhaParaProblemDetails(result.Error!);
    }

    private static IActionResult FalhaParaProblemDetails(string mensagemDeErro)
    {
        var problemDetails = new ProblemDetails
        {
            Type = TipoFalhaDeNegocio,
            Title = "Regra de negócio violada",
            Status = StatusCodes.Status400BadRequest,
            Detail = mensagemDeErro,
        };

        problemDetails.Extensions["errors"] = new[]
        {
            new { pointer = (string?)null, codigo = "regra_de_negocio_violada", mensagem = mensagemDeErro },
        };

        return new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status400BadRequest };
    }
}
