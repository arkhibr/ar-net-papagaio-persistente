using Chamados.Application.Commands;
using Chamados.Contracts;
using FluentValidation.TestHelper;
using Xunit;

namespace Chamados.Application.UnitTests.Validators;

/// <summary>
/// ReclassificarChamadoCommandValidator: validação sintática. A autorização que depende do
/// estado do agregado (técnico atribuído / equipe) mora no handler, não aqui — não é testada
/// nesta suíte.
/// </summary>
public class ReclassificarChamadoCommandValidatorTests
{
    private readonly ReclassificarChamadoCommandValidator _validator = new();

    private static ReclassificarChamadoCommand ComandoValido() => new(
        ChamadoId: Guid.NewGuid(),
        NovaPrioridade: PrioridadeChamado.Alta,
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
    public void NovaPrioridade_fora_do_enum_deve_gerar_erro()
    {
        var comando = ComandoValido() with { NovaPrioridade = (PrioridadeChamado)999 };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.NovaPrioridade);
    }

    [Fact]
    public void IdempotencyKey_vazia_deve_gerar_erro()
    {
        var comando = ComandoValido() with { IdempotencyKey = string.Empty };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.IdempotencyKey);
    }
}
