namespace SharedKernel.Messaging;

/// <summary>
/// Classificação única de exceção para nível de log (arquitetura/15-observabilidade.md, "Nível
/// de log": nunca Error para falha que já tem caminho esperado) e marcação de "já logada com
/// stack trace", para o GlobalExceptionHandler não registrar a mesma exceção duas vezes.
/// </summary>
public static class ExceptionLogging
{
    private const string ChaveJaLogada = "SharedKernel.ExceptionLogging.Logged";

    public static bool IsExpected(Exception exception) => exception is
        ValidationException
        or AuthorizationDeniedException
        or ResourceNotFoundException
        or OperationInProgressException
        or ConcurrencyException
        or PreconditionFailedException
        or IdempotencyKeyReusedException
        or OperationCanceledException;

    public static void MarkLogged(Exception exception) => exception.Data[ChaveJaLogada] = true;

    public static bool WasLogged(Exception exception) => exception.Data.Contains(ChaveJaLogada);
}
