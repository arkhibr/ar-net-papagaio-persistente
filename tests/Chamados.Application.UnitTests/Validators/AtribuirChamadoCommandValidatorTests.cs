using Chamados.Application.Commands;
using Chamados.Contracts;
using FluentValidation.TestHelper;
using Xunit;

namespace Chamados.Application.UnitTests.Validators;

/// <summary>
/// AtribuirChamadoCommandValidator: validação sintática. Autorização e concorrência
/// (RowVersion vs. valor persistido) não são checadas aqui — só a presença do RowVersion.
/// </summary>
public class AtribuirChamadoCommandValidatorTests
{
    private readonly AtribuirChamadoCommandValidator _validator = new();

    private static AtribuirChamadoCommand ComandoValido() => new(
        ChamadoId: Guid.NewGuid(),
        TecnicoId: Guid.NewGuid(),
        RowVersion: [1, 2, 3],
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
    public void TecnicoId_vazio_deve_gerar_erro()
    {
        var comando = ComandoValido() with { TecnicoId = Guid.Empty };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.TecnicoId);
    }

    [Fact]
    public void RowVersion_vazio_deve_gerar_erro()
    {
        var comando = ComandoValido() with { RowVersion = [] };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.RowVersion);
    }

    [Fact]
    public void IdempotencyKey_vazia_deve_gerar_erro()
    {
        var comando = ComandoValido() with { IdempotencyKey = string.Empty };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.IdempotencyKey);
    }
}
