using Mediator;
using SharedKernel.Messaging;
using Xunit;

namespace SharedKernel.UnitTests;

/// <summary>
/// AuthorizationBehavior (arquitetura/12-autorizacao-por-recurso.md): usa o contexto de
/// autorização do módulo dono da mensagem, tipado pela interface do módulo.
/// </summary>
public class AuthorizationBehaviorTests
{
    private interface IContextoDeTeste : IAuthorizationContext
    {
        bool Permite { get; }
    }

    private sealed class ContextoDeTeste : IContextoDeTeste
    {
        public bool Permite { get; init; }
    }

    private sealed record Mensagem(bool Ocultar = false) : IRequest<Result<Guid>>, IRequiresAuthorization<IContextoDeTeste>
    {
        public bool HideExistenceWhenDenied => Ocultar;

        public Task<bool> IsAuthorizedAsync(ICurrentUser currentUser, IContextoDeTeste context, CancellationToken cancellationToken) =>
            Task.FromResult(context.Permite);
    }

    private sealed class OutroContexto : IAuthorizationContext;

    private static AuthorizationBehavior<Mensagem, Result<Guid>> Behavior(IAuthorizationContext contexto) =>
        new(new FakeCurrentUser(), new FixedModuleService<IAuthorizationContext>(contexto));

    private static readonly MessageHandlerDelegate<Mensagem, Result<Guid>> Sucesso =
        (_, _) => ValueTask.FromResult(Result<Guid>.Success(Guid.NewGuid()));

    [Fact]
    public async Task Negado_lanca_AuthorizationDeniedException_sem_chamar_o_handler()
    {
        var chamadas = 0;

        await Assert.ThrowsAsync<AuthorizationDeniedException>(() => Behavior(new ContextoDeTeste { Permite = false })
            .Handle(new Mensagem(), (_, _) => { chamadas++; return ValueTask.FromResult(Result<Guid>.Success(Guid.NewGuid())); }, CancellationToken.None)
            .AsTask());

        Assert.Equal(0, chamadas);
    }

    [Fact]
    public async Task Negado_com_HideExistenceWhenDenied_lanca_ResourceNotFoundException()
    {
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => Behavior(new ContextoDeTeste { Permite = false })
            .Handle(new Mensagem(Ocultar: true), Sucesso, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Autorizado_chama_o_handler()
    {
        var resposta = await Behavior(new ContextoDeTeste { Permite = true }).Handle(new Mensagem(), Sucesso, CancellationToken.None);

        Assert.True(resposta.IsSuccess);
    }

    [Fact]
    public async Task Contexto_de_outro_modulo_falha_alto_em_vez_de_negar_silenciosamente()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Behavior(new OutroContexto()).Handle(new Mensagem(), Sucesso, CancellationToken.None).AsTask());
    }
}
