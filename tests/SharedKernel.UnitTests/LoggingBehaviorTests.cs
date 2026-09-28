using Mediator;
using Microsoft.Extensions.Logging;
using SharedKernel.Messaging;
using Xunit;

namespace SharedKernel.UnitTests;

/// <summary>
/// Nível de log por natureza da falha (arquitetura/15, "Nível de log"; M9 de achados.md).
/// </summary>
public class LoggingBehaviorTests
{
    private sealed record Mensagem : IRequest<Result<Guid>>;

    private sealed class LoggerDeTeste : ILogger<LoggingBehavior<Mensagem, Result<Guid>>>
    {
        public List<(LogLevel Nivel, Exception? Excecao)> Registros { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Registros.Add((logLevel, exception));
    }

    public static TheoryData<Exception> FalhasEsperadas() =>
    [
        new ValidationException([]),
        new AuthorizationDeniedException("x"),
        new ResourceNotFoundException("x"),
        new OperationInProgressException("x"),
        new ConcurrencyException("x"),
        new PreconditionFailedException("x"),
        new IdempotencyKeyReusedException("x"),
    ];

    [Theory]
    [MemberData(nameof(FalhasEsperadas))]
    public async Task Falha_com_caminho_esperado_nunca_e_logada_como_Error(Exception falha)
    {
        var logger = new LoggerDeTeste();

        await Assert.ThrowsAnyAsync<Exception>(() => new LoggingBehavior<Mensagem, Result<Guid>>(logger)
            .Handle(new Mensagem(), (_, _) => throw falha, CancellationToken.None).AsTask());

        Assert.DoesNotContain(logger.Registros, r => r.Nivel >= LogLevel.Warning);
        Assert.False(ExceptionLogging.WasLogged(falha));
    }

    [Fact]
    public async Task Falha_inesperada_e_Error_com_stack_trace_e_marcada_como_ja_logada()
    {
        var logger = new LoggerDeTeste();
        var falha = new InvalidOperationException("bug");

        await Assert.ThrowsAsync<InvalidOperationException>(() => new LoggingBehavior<Mensagem, Result<Guid>>(logger)
            .Handle(new Mensagem(), (_, _) => throw falha, CancellationToken.None).AsTask());

        Assert.Contains(logger.Registros, r => r.Nivel == LogLevel.Error && r.Excecao == falha);
        Assert.True(ExceptionLogging.WasLogged(falha));
    }
}
