using FluentValidation;
using Mediator;
using SharedKernel;
using SharedKernel.Messaging;
using Xunit;

namespace SharedKernel.UnitTests;

/// <summary>
/// ValidationBehavior (arquitetura/16-validacao-sintatica-vs-invariante.md,
/// arquitetura/03-commands-e-queries.md):
///
/// 1. Sem nenhum IValidator&lt;TMessage&gt; registrado, passa direto para next() sem custo.
/// 2. Com validators, agrega TODAS as falhas de uma vez (nunca para na primeira) antes de
///    lançar SharedKernel.ValidationException.
/// 3. Entrada válida chama o handler normalmente.
/// </summary>
public class ValidationBehaviorTests
{
    private sealed record MensagemDeTeste(string Nome, int Quantidade) : IRequest<Result<Guid>>;

    private sealed class NomeNaoPodeSerVazioValidator : AbstractValidator<MensagemDeTeste>
    {
        public NomeNaoPodeSerVazioValidator()
        {
            RuleFor(m => m.Nome).NotEmpty().WithMessage("Nome é obrigatório.");
        }
    }

    private sealed class QuantidadeDevePositivaValidator : AbstractValidator<MensagemDeTeste>
    {
        public QuantidadeDevePositivaValidator()
        {
            RuleFor(m => m.Quantidade).GreaterThan(0).WithMessage("Quantidade deve ser maior que zero.");
        }
    }

    [Fact]
    public async Task Sem_nenhum_validator_registrado_deve_chamar_o_handler_diretamente()
    {
        var behavior = new ValidationBehavior<MensagemDeTeste, Result<Guid>>(
            Array.Empty<IValidator<MensagemDeTeste>>());
        var mensagem = new MensagemDeTeste("", 0); // seria inválida se houvesse validator
        var valorGerado = Guid.NewGuid();

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next =
            (_, _) => ValueTask.FromResult(Result<Guid>.Success(valorGerado));

        var resposta = await behavior.Handle(mensagem, next, CancellationToken.None);

        Assert.True(resposta.IsSuccess);
        Assert.Equal(valorGerado, resposta.Value);
    }

    [Fact]
    public async Task Com_multiplos_validators_e_multiplas_falhas_deve_agregar_todas_antes_de_lancar()
    {
        var validators = new IValidator<MensagemDeTeste>[]
        {
            new NomeNaoPodeSerVazioValidator(),
            new QuantidadeDevePositivaValidator(),
        };
        var behavior = new ValidationBehavior<MensagemDeTeste, Result<Guid>>(validators);
        var mensagem = new MensagemDeTeste("", 0); // viola as duas regras ao mesmo tempo
        var chamadasAoHandler = 0;

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next = (_, _) =>
        {
            chamadasAoHandler++;
            return ValueTask.FromResult(Result<Guid>.Success(Guid.NewGuid()));
        };

        var excecao = await Assert.ThrowsAsync<SharedKernel.ValidationException>(
            () => behavior.Handle(mensagem, next, CancellationToken.None).AsTask());

        Assert.Equal(0, chamadasAoHandler); // handler nunca roda com entrada mal formada
        Assert.True(
            excecao.Errors.Count >= 2,
            $"Esperava ao menos 2 erros agregados, encontrou {excecao.Errors.Count}: " +
            string.Join("; ", excecao.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
        Assert.Contains(excecao.Errors, e => e.PropertyName == nameof(MensagemDeTeste.Nome));
        Assert.Contains(excecao.Errors, e => e.PropertyName == nameof(MensagemDeTeste.Quantidade));
    }

    [Fact]
    public async Task Entrada_valida_deve_chamar_o_handler_normalmente()
    {
        var validators = new IValidator<MensagemDeTeste>[]
        {
            new NomeNaoPodeSerVazioValidator(),
            new QuantidadeDevePositivaValidator(),
        };
        var behavior = new ValidationBehavior<MensagemDeTeste, Result<Guid>>(validators);
        var mensagem = new MensagemDeTeste("válido", 10);
        var valorGerado = Guid.NewGuid();

        MessageHandlerDelegate<MensagemDeTeste, Result<Guid>> next =
            (_, _) => ValueTask.FromResult(Result<Guid>.Success(valorGerado));

        var resposta = await behavior.Handle(mensagem, next, CancellationToken.None);

        Assert.True(resposta.IsSuccess);
        Assert.Equal(valorGerado, resposta.Value);
    }
}
