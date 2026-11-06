using Chamados.Application.Commands;
using Chamados.Contracts;
using FluentValidation.TestHelper;
using Xunit;

namespace Chamados.Application.UnitTests.Validators;

/// <summary>
/// EscalonarChamadoCommandValidator: validação sintática. IdempotencyKey aqui é gerada pelo
/// próprio job (nunca header HTTP), mas é validada como um campo comum.
/// </summary>
public class EscalonarChamadoCommandValidatorTests
{
    private readonly EscalonarChamadoCommandValidator _validator = new();

    private static EscalonarChamadoCommand ComandoValido() => new(
        ChamadoId: Guid.NewGuid(),
        IdempotencyKey: "chave-job-1");

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
