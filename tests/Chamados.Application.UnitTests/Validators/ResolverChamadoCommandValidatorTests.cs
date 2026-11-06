using Chamados.Application.Commands;
using Chamados.Contracts;
using FluentValidation.TestHelper;
using Xunit;

namespace Chamados.Application.UnitTests.Validators;

/// <summary>
/// ResolverChamadoCommandValidator: NotaResolucao é obrigatória (validação sintática,
/// plano-de-arquitetura.md secao 2). Nulo, vazio e whitespace-only devem todos falhar
/// (duplicação intencional com Chamado.Resolver, que também rejeita
/// string.IsNullOrWhiteSpace — o validador existe para agregar erros na API,
/// o agregado precisa continuar seguro para qualquer chamador).
/// </summary>
public class ResolverChamadoCommandValidatorTests
{
    private readonly ResolverChamadoCommandValidator _validator = new();

    private static ResolverChamadoCommand ComandoValido() => new(
        ChamadoId: Guid.NewGuid(),
        NotaResolucao: "Reiniciei o serviço e o problema não voltou a ocorrer.",
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
    public void NotaResolucao_nula_deve_gerar_erro()
    {
        var comando = ComandoValido() with { NotaResolucao = null! };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.NotaResolucao);
    }

    [Fact]
    public void NotaResolucao_vazia_deve_gerar_erro()
    {
        var comando = ComandoValido() with { NotaResolucao = string.Empty };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.NotaResolucao);
    }

    [Fact]
    public void NotaResolucao_apenas_com_espacos_em_branco_deve_gerar_erro()
    {
        var comando = ComandoValido() with { NotaResolucao = "   " };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.NotaResolucao);
    }

    [Fact]
    public void IdempotencyKey_vazia_deve_gerar_erro()
    {
        var comando = ComandoValido() with { IdempotencyKey = string.Empty };

        var resultado = _validator.TestValidate(comando);

        resultado.ShouldHaveValidationErrorFor(c => c.IdempotencyKey);
    }
}
