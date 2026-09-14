using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.Infrastructure;

/// <summary>
/// Tabela exceção -> status HTTP (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md).
/// IExceptionHandler nativo (.NET 8+), não middleware artesanal — corpo em RFC 9457
/// (ProblemDetails), com extensão "errors[]" quando aplicável. Único lugar da solution que
/// conhece essa tabela; controllers nunca reimplementam tratamento de erro (Result.Failure é
/// tratado à parte, por ResultExtensions.ToActionResult, porque não é exceção).
///
/// | Exceção                        | Status | Nota                                             |
/// |---------------------------------|--------|---------------------------------------------------|
/// | SharedKernel.ValidationException| 400    | errors[] com um item por ValidationError           |
/// | DomainException                 | 400    | anomalia (rede de segurança, não caminho esperado) |
/// | AuthorizationDeniedException    | 403    | leitura B de autorização (pipeline)                |
/// | OperationInProgressException    | 409    | mesma Idempotency-Key, operação anterior em curso  |
/// | ConcurrencyException            | 409    | conflito de RowVersion detectado no SaveChanges    |
/// | qualquer outra                  | 500    | sem detalhe exposto (nunca stack trace no corpo)   |
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            SharedKernel.ValidationException validationException => MontarValidacao(validationException),
            DomainException domainException => MontarDominio(domainException),
            AuthorizationDeniedException authorizationDeniedException => MontarAutorizacao(authorizationDeniedException),
            OperationInProgressException operationInProgressException => MontarOperacaoEmAndamento(operationInProgressException),
            ConcurrencyException concurrencyException => MontarConcorrencia(concurrencyException),
            _ => null,
        };

        if (problemDetails is null)
        {
            // Qualquer outra exceção não prevista: 500, log completo no servidor (nunca no
            // corpo da resposta), ProblemDetails genérico sem "errors[]" nem stack trace.
            _logger.LogError(exception, "Erro não tratado processando {Path}", httpContext.Request.Path);

            problemDetails = new ProblemDetails
            {
                Type = "https://arkhi.com.br/erros/erro-interno",
                Title = "Erro interno",
                Status = StatusCodes.Status500InternalServerError,
                Instance = httpContext.Request.Path,
            };
        }
        else
        {
            problemDetails.Instance = httpContext.Request.Path;

            if (exception is DomainException)
            {
                // DomainException não capturada chegando aqui é anomalia de implementação
                // (arquitetura/06: "o caminho esperado é virar Result.Failure, não chegar
                // aqui"). Logado como aviso para detectar o handler que esqueceu o
                // try/catch, sem tratar como bug de infraestrutura (500).
                _logger.LogWarning(
                    exception,
                    "DomainException não capturada no handler chegou ao GlobalExceptionHandler " +
                    "(anomalia: deveria ter virado Result<T>.Failure) processando {Path}",
                    httpContext.Request.Path);
            }
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }

    private static ProblemDetails MontarValidacao(SharedKernel.ValidationException exception)
    {
        var problemDetails = new ProblemDetails
        {
            Type = "https://arkhi.com.br/erros/validacao",
            Title = "Um ou mais campos são inválidos",
            Status = StatusCodes.Status400BadRequest,
            Detail = exception.Message,
        };

        problemDetails.Extensions["errors"] = exception.Errors
            .Select(erro => new { pointer = erro.PropertyName, codigo = "formato_invalido", mensagem = erro.ErrorMessage })
            .ToArray();

        return problemDetails;
    }

    private static ProblemDetails MontarDominio(DomainException exception)
    {
        var problemDetails = new ProblemDetails
        {
            Type = "https://arkhi.com.br/erros/regra-de-negocio-violada",
            Title = "Regra de negócio violada",
            Status = StatusCodes.Status400BadRequest,
            Detail = exception.Message,
        };

        problemDetails.Extensions["errors"] = new[]
        {
            new { pointer = (string?)null, codigo = "estado_invalido", mensagem = exception.Message },
        };

        return problemDetails;
    }

    private static ProblemDetails MontarAutorizacao(AuthorizationDeniedException exception) => new()
    {
        Type = "https://arkhi.com.br/erros/nao-autorizado",
        Title = "Não autorizado",
        Status = StatusCodes.Status403Forbidden,
        Detail = exception.Message,
    };

    private static ProblemDetails MontarOperacaoEmAndamento(OperationInProgressException exception) => new()
    {
        Type = "https://arkhi.com.br/erros/operacao-em-andamento",
        Title = "Operação em andamento",
        Status = StatusCodes.Status409Conflict,
        Detail = exception.Message,
    };

    private static ProblemDetails MontarConcorrencia(ConcurrencyException exception) => new()
    {
        Type = "https://arkhi.com.br/erros/conflito-de-concorrencia",
        Title = "Conflito de concorrência",
        Status = StatusCodes.Status409Conflict,
        Detail = exception.Message,
    };
}
