using Mediator;
using SharedKernel.Messaging;
using Xunit;

namespace SharedKernel.UnitTests;

/// <summary>
/// UnitOfWorkBehavior sem idempotência (arquitetura/25): commit único, só quando o handler
/// devolve sucesso. Com idempotência, ver IdempotencyBehaviorTests.
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

        public void DiscardChanges()
        {
        }
    }

    private static UnitOfWorkBehavior<MensagemDeTeste, Result<Guid>> Behavior(FakeUnitOfWork unitOfWork) =>
        new(new FixedModuleService<IUnitOfWork>(unitOfWork), new PendingIdempotency());

    [Fact]
    public async Task Quando_o_handler_devolve_sucesso_deve_chamar_SaveChangesAsync_uma_vez()
    {
        var unitOfWork = new FakeUnitOfWork();

        var resposta = await Behavior(unitOfWork).Handle(
            new MensagemDeTeste(), (_, _) => ValueTask.FromResult(Result<Guid>.Success(Guid.NewGuid())), CancellationToken.None);

        Assert.True(resposta.IsSuccess);
        Assert.Equal(1, unitOfWork.ChamadasDeSaveChanges);
    }

    [Fact]
    public async Task Quando_o_handler_devolve_Result_Failure_nao_deve_chamar_SaveChangesAsync()
    {
        var unitOfWork = new FakeUnitOfWork();

        var resposta = await Behavior(unitOfWork).Handle(
            new MensagemDeTeste(), (_, _) => ValueTask.FromResult(Result<Guid>.Failure("Regra de negócio violada.")), CancellationToken.None);

        Assert.True(resposta.IsFailure);
        Assert.Equal(0, unitOfWork.ChamadasDeSaveChanges);
    }

    [Fact]
    public async Task Quando_o_handler_lanca_excecao_nao_deve_chamar_SaveChangesAsync_e_a_excecao_deve_propagar()
    {
        var unitOfWork = new FakeUnitOfWork();

        await Assert.ThrowsAsync<ConcurrencyException>(() => Behavior(unitOfWork).Handle(
            new MensagemDeTeste(), (_, _) => throw new ConcurrencyException("conflito"), CancellationToken.None).AsTask());

        Assert.Equal(0, unitOfWork.ChamadasDeSaveChanges);
    }
}
