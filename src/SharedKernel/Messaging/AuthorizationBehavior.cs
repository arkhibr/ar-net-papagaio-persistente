using Mediator;

namespace SharedKernel.Messaging;

/// <summary>
/// Terceiro elo da ordem fixa (Logging → Validation → Authorization → Idempotency → Caching →
/// Handler, arquitetura/03-commands-e-queries.md). Roda só para TMessage : IRequiresAuthorization
/// (constraint no generic — nem precisa checar tipo em runtime, diferente de
/// Logging/Validation que rodam para toda mensagem). Depois de Validation (não vale gastar
/// checagem de permissão numa entrada mal formada) e antes de Idempotency (uma tentativa não
/// autorizada não deveria reservar uma chave de idempotência) — essa ordem é responsabilidade
/// de quem registra os behaviors (SharedKernelPipelineExtensions), não deste generic constraint.
///
/// Implementação de referência: arquitetura/12-autorizacao-por-recurso.md.
/// </summary>
public sealed class AuthorizationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage, IRequiresAuthorization
{
    private readonly ICurrentUser _currentUser;
    private readonly IAuthorizationContext _context;

    public AuthorizationBehavior(ICurrentUser currentUser, IAuthorizationContext context)
    {
        _currentUser = currentUser;
        _context = context;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (!await message.IsAuthorizedAsync(_currentUser, _context, cancellationToken))
        {
            throw new AuthorizationDeniedException("Usuário não tem permissão para executar esta operação.");
        }

        return await next(message, cancellationToken);
    }
}
