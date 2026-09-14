using Mediator;
using SharedKernel;
using SharedKernel.Messaging;
using Xunit;

namespace SharedKernel.UnitTests;

/// <summary>
/// AuthorizationBehavior (arquitetura/12-autorizacao-por-recurso.md,
/// arquitetura/03-commands-e-queries.md): usa um ICurrentUser fake (nunca HttpContext real,
/// 13-estrategia-de-testes.md).
///
/// 1. IsAuthorizedAsync == false lança AuthorizationDeniedException e o handler nunca roda.
/// 2. IsAuthorizedAsync == true chama o handler normalmente.
/// </summary>
public class AuthorizationBehaviorTests
{
    private sealed record MensagemDeTeste(Guid RecursoId, bool Autorizado)
        : IRequest<Result<Guid>>, IRequiresAuthorization
    {
        public Task<bool> IsAuthorizedAsync(
            ICurrentUser currentUser, IAuthorizationContext context, CancellationToken cancellationToken) =>
            Task.FromResult(Autorizado);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId { get; init; } = Guid.NewGuid();
        public Guid SessionId { get; init; } = Guid.NewGuid();
        public bool IsAuthenticated { get; init; } = true;
        public bool IsSystemActor { get; init; }
        public bool IsInRole(string role) => false;
    }

    private sealed class FakeAuthorizationContext : IAuthorizationContext
    {
        public Task<bool> HasResourceLinkAsync(Guid userId, Guid resourceId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    [Fact]
    public async Task Quando_IsAuthorizedAsync_devolve_false_deve_lancar_AuthorizationDeniedException_sem_chamar_o_handler()
    {
        var behavior = new AuthorizationBehavior<MensagemDeTeste, Result<Guid>>(
            new FakeCurrentUser(), new FakeAuthorizationContext());
        var mensagem = new MensagemDeTeste(Guid.NewGuid(), Autorizado: false);
        var chamadasAoHandler = 0;

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next = (_, _) =>
        {
            chamadasAoHandler++;
            return ValueTask.FromResult(Result<Guid>.Success(Guid.NewGuid()));
        };

        await Assert.ThrowsAsync<AuthorizationDeniedException>(
            () => behavior.Handle(mensagem, next, CancellationToken.None).AsTask());

        Assert.Equal(0, chamadasAoHandler);
    }

    [Fact]
    public async Task Quando_IsAuthorizedAsync_devolve_true_deve_chamar_o_handler_normalmente()
    {
        var behavior = new AuthorizationBehavior<MensagemDeTeste, Result<Guid>>(
            new FakeCurrentUser(), new FakeAuthorizationContext());
        var mensagem = new MensagemDeTeste(Guid.NewGuid(), Autorizado: true);
        var valorGerado = Guid.NewGuid();

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next =
            (_, _) => ValueTask.FromResult(Result<Guid>.Success(valorGerado));

        var resposta = await behavior.Handle(mensagem, next, CancellationToken.None);

        Assert.True(resposta.IsSuccess);
        Assert.Equal(valorGerado, resposta.Value);
    }
}
