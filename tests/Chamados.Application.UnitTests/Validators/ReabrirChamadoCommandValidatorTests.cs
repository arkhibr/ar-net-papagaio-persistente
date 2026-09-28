using Chamados.Application.Commands;
using Chamados.Contracts;
using FluentValidation.TestHelper;
using Xunit;

namespace Chamados.Application.UnitTests.Validators;

/// <summary>
/// ReabrirChamadoCommandValidator: validação sintática. A janela de 5 dias corridos do
/// fechamento depende do estado do agregado e continua invariante em Chamado.Reabrir,
/// não é testada aqui.
/// </summary>
public class ReabrirChamadoCommandValidatorTests
{
    private readonly ReabrirChamadoCommandValidator _validator = new();

    private static ReabrirChamadoCommand ComandoValido() => new(
        ChamadoId: Guid.NewGuid(),
        IdempotencyKey: "chave-1");

    [Fact]
    public void Comando_valido_nao_deve_gerar_erro()
    {
        var resultado = _validator.TestValidate(ComandoValido());

        resultado.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ChamadoId_vazio_deve_gerar_erro()
    {
        var comando = ComandoValido() with { ChamadoId = Guid.Empty };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.ChamadoId);
    }

    [Fact]
    public void IdempotencyKey_vazia_deve_gerar_erro()
    {
        var comando = ComandoValido() with { IdempotencyKey = string.Empty };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.IdempotencyKey);
    }
}
