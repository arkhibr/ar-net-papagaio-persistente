using Microsoft.AspNetCore.Diagnostics;
using SharedKernel;
using SharedKernel.Messaging;

namespace Api.Infrastructure;

/// <summary>
/// Tabela exceção → status HTTP (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md),
/// com o corpo montado por HttpErrors e escrito pelo serviço nativo de Problem Details.
/// Falha de negócio determinística não passa por aqui: é Result.Failure, traduzido por
/// ResultExtensions.
///
/// Log (arquitetura/15): só exceção inesperada (500) é Error, e só se o LoggingBehavior ainda não
/// a registrou com stack trace; DomainException que escapou do handler é Warning (anomalia);
/// as demais têm caminho esperado e não são logadas aqui.
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
            SharedKernel.ValidationException validacao => HttpErrors.Validation(validacao.Errors),
            DomainException dominio => HttpErrors.DomainAnomaly(dominio.Message),
            AuthorizationDeniedException negado => HttpErrors.Forbidden(negado.Message),
            ResourceNotFoundException => HttpErrors.NotFound(),
            OperationInProgressException emAndamento => HttpErrors.OperationInProgress(emAndamento.Message),
            ConcurrencyException concorrencia => HttpErrors.Concurrency(concorrencia.Message),
            PreconditionFailedException precondicao => HttpErrors.PreconditionFailed(precondicao.Message),
            IdempotencyKeyReusedException chaveReutilizada => HttpErrors.IdempotencyKeyReused(chaveReutilizada.Message),
            _ => null,
        };

        if (problemDetails is null)
        {
            if (!ExceptionLogging.WasLogged(exception))
            {
                _logger.LogError(exception, "Erro não tratado processando {Path}", httpContext.Request.Path);
            }

            problemDetails = HttpErrors.Internal();
        }
        else if (exception is DomainException && !ExceptionLogging.WasLogged(exception))
        {
            _logger.LogWarning(
                exception,
                "DomainException não capturada no handler chegou ao GlobalExceptionHandler " +
                "(anomalia: deveria ter virado Result<T>.Failure) processando {Path}",
                httpContext.Request.Path);
        }

        await ProblemResult.WriteAsync(httpContext, problemDetails);

        return true;
    }
}
