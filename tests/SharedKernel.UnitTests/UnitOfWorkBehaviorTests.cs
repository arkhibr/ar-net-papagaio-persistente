using Mediator;
using SharedKernel;
using SharedKernel.Messaging;
using Xunit;

namespace SharedKernel.UnitTests;

/// <summary>
/// UnitOfWorkBehavior (arquitetura/25-transacao-e-unit-of-work.md): commit único, só quando o
/// handler devolve sucesso. Result.Failure faz rollback implícito (SaveChanges nunca chamado).
/// </summary>
public class UnitOfWorkBehaviorTests
{
    private sealed record MensagemDeTeste : IRequest<Result<Guid>>, ITransactionalCommand;

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int ChamadasDeSaveChanges { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            ChamadasDeSaveChanges++;
            return Task.FromResult(1);
        }
    }

    [Fact]
    public async Task Quando_o_handler_devolve_sucesso_deve_chamar_SaveChangesAsync_uma_vez()
    {
        var unitOfWork = new FakeUnitOfWork();
        var behavior = new UnitOfWorkBehavior<MensagemDeTeste, Result<Guid>>(unitOfWork);
        var mensagem = new MensagemDeTeste();

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next =
            (_, _) => ValueTask.FromResult(Result<Guid>.Success(Guid.NewGuid()));

        var resposta = await behavior.Handle(mensagem, next, CancellationToken.None);

        Assert.True(resposta.IsSuccess);
        Assert.Equal(1, unitOfWork.ChamadasDeSaveChanges);
    }

    [Fact]
    public async Task Quando_o_handler_devolve_Result_Failure_nao_deve_chamar_SaveChangesAsync()
    {
        var unitOfWork = new FakeUnitOfWork();
        var behavior = new UnitOfWorkBehavior<MensagemDeTeste, Result<Guid>>(unitOfWork);
        var mensagem = new MensagemDeTeste();

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next =
            (_, _) => ValueTask.FromResult(Result<Guid>.Failure("Regra de negócio violada."));

        var resposta = await behavior.Handle(mensagem, next, CancellationToken.None);

        Assert.True(resposta.IsFailure);
        Assert.Equal(0, unitOfWork.ChamadasDeSaveChanges);
    }

    [Fact]
    public async Task Quando_o_handler_lanca_excecao_nao_deve_chamar_SaveChangesAsync_e_a_excecao_deve_propagar()
    {
        var unitOfWork = new FakeUnitOfWork();
        var behavior = new UnitOfWorkBehavior<MensagemDeTeste, Result<Guid>>(unitOfWork);
        var mensagem = new MensagemDeTeste();

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next =
            (_, _) => throw new ConcurrencyException("O recurso foi alterado por outra operação.");

        await Assert.ThrowsAsync<ConcurrencyException>(
            () => behavior.Handle(mensagem, next, CancellationToken.None).AsTask());

        Assert.Equal(0, unitOfWork.ChamadasDeSaveChanges);
    }
}
