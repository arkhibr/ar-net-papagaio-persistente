using FluentValidation;
using Mediator;

namespace SharedKernel.Messaging;

/// <summary>
/// Segundo elo da ordem fixa (Logging → Validation → Authorization → Idempotency → Caching →
/// Handler, arquitetura/03-commands-e-queries.md). Roda para toda mensagem, sem constraint de
/// interface: resolve IEnumerable&lt;FluentValidation.IValidator&lt;TMessage&gt;&gt; via DI — se
/// vazio (mensagem sem AbstractValidator&lt;TMessage&gt; registrado), passa direto para next()
/// sem custo, mesma mecânica opt-in dos demais behaviors.
///
/// Validação sintática (arquitetura/16-validacao-sintatica-vs-invariante.md): depende só do
/// valor de cada campo isolado. Roda TODOS os validators e agrega TODAS as falhas de uma vez
/// (nunca para no primeiro validator nem na primeira regra) antes de lançar
/// SharedKernel.ValidationException — nunca deixa a checagem parar cedo, porque o corpo RFC
/// 9457 do lado Api espera um `errors[]` completo (arquitetura/06).
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IEnumerable<IValidator<TMessage>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TMessage>> validators)
    {
        _validators = validators;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next(message, cancellationToken);
        }

        var context = new ValidationContext<TMessage>(message);

        var resultados = await Task.WhenAll(
            _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var falhas = resultados
            .SelectMany(resultado => resultado.Errors)
            .Where(falha => falha is not null)
            .ToList();

        if (falhas.Count > 0)
        {
            var erros = falhas
                .Select(falha => new ValidationError(falha.PropertyName, falha.ErrorMessage))
                .ToList();

            // Qualificado explicitamente: FluentValidation também declara um ValidationException
            // (using FluentValidation acima), e este behavior precisa lançar sempre o tipo do
            // SharedKernel (que carrega a lista agregada de ValidationError, não o tipo da lib).
            throw new SharedKernel.ValidationException(erros);
        }

        return await next(message, cancellationToken);
    }
}
