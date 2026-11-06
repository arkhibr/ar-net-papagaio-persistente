using Mediator;
using SharedKernel.Modules;

namespace SharedKernel.Messaging;

/// <summary>
/// Terceiro elo da ordem fixa (Logging → Validation → Authorization → Idempotency → Caching →
/// UnitOfWork → Handler, arquitetura/03-commands-e-queries.md). Roda só para
/// TMessage : IRequiresAuthorization, com o IAuthorizationContext do módulo dono da mensagem.
/// Depois de Validation (não vale gastar checagem de permissão numa entrada mal formada) e antes
/// de Idempotency (uma tentativa não autorizada não reserva chave).
///
/// Implementação de referência: arquitetura/12-autorizacao-por-recurso.md.
/// </summary>
public sealed class AuthorizationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage, IRequiresAuthorization
{
    private readonly ICurrentUser _currentUser;
    private readonly IModuleService<IAuthorizationContext> _contexts;

    public AuthorizationBehavior(ICurrentUser currentUser, IModuleService<IAuthorizationContext> contexts)
    {
        _currentUser = currentUser;
        _contexts = contexts;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var context = _contexts.For(typeof(TMessage));

        if (!await message.IsAuthorizedAsync(_currentUser, context, cancellationToken))
        {
            if (message.HideExistenceWhenDenied)
            {
                throw new ResourceNotFoundException("Recurso não encontrado.");
            }

            throw new AuthorizationDeniedException("Usuário não tem permissão para executar esta operação.");
        }

        return await next(message, cancellationToken);
    }
}
