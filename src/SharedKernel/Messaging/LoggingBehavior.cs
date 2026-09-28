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
            // Log e rethrow: a exceção nunca é mascarada aqui. Nível pela natureza da falha
            // (arquitetura/15): falha com caminho esperado (400/403/404/409/412/422) é
            // Information, sem stack trace; DomainException que escapou do handler é anomalia
            // (Warning); o resto é Error com stack trace, marcado para o GlobalExceptionHandler
            // não repetir o registro.
            var duracaoMs = System.Diagnostics.Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;

            if (ExceptionLogging.IsExpected(ex))
            {
                _logger.LogInformation(
                    "Falha esperada em {Mensagem} após {DuracaoMs}ms: {TipoDeFalha}",
                    nomeDaMensagem, duracaoMs, ex.GetType().Name);
            }
            else if (ex is DomainException)
            {
                _logger.LogWarning(
                    ex, "DomainException não capturada pelo handler de {Mensagem} após {DuracaoMs}ms",
                    nomeDaMensagem, duracaoMs);
                ExceptionLogging.MarkLogged(ex);
            }
            else
            {
                _logger.LogError(ex, "Falha em {Mensagem} após {DuracaoMs}ms", nomeDaMensagem, duracaoMs);
                ExceptionLogging.MarkLogged(ex);
            }

            throw;
        }
    }
}
