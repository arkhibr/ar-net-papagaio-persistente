using Mediator;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Messaging;

/// <summary>
/// Primeiro elo da ordem fixa de pipeline behaviors (arquitetura/03-commands-e-queries.md:
/// Logging → Validation → Authorization → Idempotency → Caching → (UnitOfWork →) Handler).
/// Roda para toda mensagem (Command ou Query), sem constraint de interface — é por isso que
/// vem primeiro: nenhum outro behavior decide se este roda ou não.
///
/// Não loga o payload da mensagem inteira (pode carregar dado sensível de qualquer módulo,
/// e este tipo é SharedKernel — nunca pode conhecer campo de negócio específico como
/// "ChamadoId"). Loga só o nome do tipo da mensagem e a duração; log de exceção (e rethrow,
/// nunca mascarada) é aceitável aqui porque não substitui o GlobalExceptionHandler, só
/// registra que algo propagou por este ponto do pipeline.
/// </summary>
public sealed class LoggingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ILogger<LoggingBehavior<TMessage, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var nomeDaMensagem = typeof(TMessage).Name;
        var inicio = System.Diagnostics.Stopwatch.GetTimestamp();

        _logger.LogInformation("Iniciando {Mensagem}", nomeDaMensagem);

        try
        {
            var response = await next(message, cancellationToken);

            _logger.LogInformation(
                "Concluído {Mensagem} em {DuracaoMs}ms",
                nomeDaMensagem,
                System.Diagnostics.Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            // Log e rethrow: a exceção nunca é mascarada aqui, só registrada. O tratamento
            // (Result.Failure vs. exceção -> status HTTP) continua responsabilidade do handler
            // e do GlobalExceptionHandler (arquitetura/06-contrato-erro-http-idempotencia-e-concorrencia.md).
            _logger.LogError(
                ex,
                "Falha em {Mensagem} após {DuracaoMs}ms",
                nomeDaMensagem,
                System.Diagnostics.Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);

            throw;
        }
    }
}
