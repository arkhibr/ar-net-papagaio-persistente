using Microsoft.AspNetCore.Mvc;

namespace Api.Infrastructure;

/// <summary>
/// Escreve um ProblemDetails pelo serviço nativo (IProblemDetailsService, AddProblemDetails()),
/// como pede arquitetura/06, "Implementação": application/problem+json, instance e traceId
/// preenchidos do mesmo jeito para exceção, Result e erro de model binding.
/// </summary>
internal sealed class ProblemResult : IActionResult
{
    private readonly ProblemDetails _problemDetails;

    public ProblemResult(ProblemDetails problemDetails)
    {
        _problemDetails = problemDetails;
    }

    public Task ExecuteResultAsync(ActionContext context) => WriteAsync(context.HttpContext, _problemDetails);

    public static async Task WriteAsync(HttpContext httpContext, ProblemDetails problemDetails)
    {
        problemDetails.Instance ??= httpContext.Request.Path;
        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        var service = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        var escrito = await service.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
        });

        if (!escrito)
        {
            // Accept do cliente não aceita JSON: ainda assim responde o corpo padrão.
            await httpContext.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json");
        }
    }
}
