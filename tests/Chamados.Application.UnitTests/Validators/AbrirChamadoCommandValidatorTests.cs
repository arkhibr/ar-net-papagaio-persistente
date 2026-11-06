using Chamados.Application.Commands;
using Chamados.Contracts;
using FluentValidation.TestHelper;
using Xunit;

namespace Chamados.Application.UnitTests.Validators;

/// <summary>
/// AbrirChamadoCommandValidator: validação sintática, depende só do valor de cada campo
/// (arquitetura/16-validacao-sintatica-vs-invariante.md). Usamos FluentValidation.TestHelper
/// (TestValidate) para asserções por campo.
/// </summary>
public class AbrirChamadoCommandValidatorTests
{
    private readonly AbrirChamadoCommandValidator _validator = new();

    private static AbrirChamadoCommand ComandoValido() => new(
        SolicitanteId: Guid.NewGuid(),
        CategoriaId: Guid.NewGuid(),
        Prioridade: PrioridadeChamado.Media,
        IdempotencyKey: "chave-1");

    [Fact]
    public void Comando_valido_nao_deve_gerar_erro()
    {
        var resultado = _validator.TestValidate(ComandoValido());

        resultado.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void SolicitanteId_vazio_deve_gerar_erro()
    {
        var comando = ComandoValido() with { SolicitanteId = Guid.Empty };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.SolicitanteId);
    }

    [Fact]
    public void CategoriaId_vazio_deve_gerar_erro()
    {
        var comando = ComandoValido() with { CategoriaId = Guid.Empty };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.CategoriaId);
    }

    [Fact]
    public void Prioridade_fora_do_enum_deve_gerar_erro()
    {
        var comando = ComandoValido() with { Prioridade = (PrioridadeChamado)999 };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.Prioridade);
    }

    [Fact]
    public void IdempotencyKey_vazia_deve_gerar_erro()
    {
        var comando = ComandoValido() with { IdempotencyKey = string.Empty };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.IdempotencyKey);
    }
}
