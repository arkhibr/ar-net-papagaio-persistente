using Chamados.Contracts;
using FluentValidation;

namespace Chamados.Application.Commands;

/// <summary>
/// Validação sintática (arquitetura/16-validacao-sintatica-vs-invariante.md).
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class FecharChamadoCommandValidator : AbstractValidator<FecharChamadoCommand>
{
    public FecharChamadoCommandValidator()
    {
        RuleFor(c => c.ChamadoId).NotEmpty();
        RuleFor(c => c.SolicitanteId).NotEmpty();
        RuleFor(c => c.IdempotencyKey).NotEmpty();
    }
}
