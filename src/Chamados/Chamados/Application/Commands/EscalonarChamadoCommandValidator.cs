using Chamados.Contracts;
using FluentValidation;

namespace Chamados.Application.Commands;

/// <summary>
/// Validação sintática (arquitetura/16-validacao-sintatica-vs-invariante.md). IdempotencyKey
/// aqui é gerada pelo próprio job (não header HTTP), mas continua sendo um valor de campo como
/// outro qualquer para fins de validação sintática.
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class EscalonarChamadoCommandValidator : AbstractValidator<EscalonarChamadoCommand>
{
    public EscalonarChamadoCommandValidator()
    {
        RuleFor(c => c.ChamadoId).NotEmpty();
        RuleFor(c => c.IdempotencyKey).NotEmpty();
    }
}
