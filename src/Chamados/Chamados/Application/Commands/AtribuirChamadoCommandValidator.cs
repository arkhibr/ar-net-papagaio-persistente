using Chamados.Contracts;
using FluentValidation;

namespace Chamados.Application.Commands;

/// <summary>
/// Validação sintática (arquitetura/16-validacao-sintatica-vs-invariante.md): depende só do
/// valor de cada campo, isolado. A autorização (papel Técnico + membro da equipe) e a
/// concorrência (RowVersion vs. o valor persistido) não são checadas aqui — não são sintáticas.
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class AtribuirChamadoCommandValidator : AbstractValidator<AtribuirChamadoCommand>
{
    public AtribuirChamadoCommandValidator()
    {
        RuleFor(c => c.ChamadoId).NotEmpty();
        RuleFor(c => c.TecnicoId).NotEmpty();
        RuleFor(c => c.RowVersion).NotEmpty();
        RuleFor(c => c.IdempotencyKey).NotEmpty();
    }
}
