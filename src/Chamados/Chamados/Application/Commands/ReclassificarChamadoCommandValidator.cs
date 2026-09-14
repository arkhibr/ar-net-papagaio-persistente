using Chamados.Contracts;
using FluentValidation;

namespace Chamados.Application.Commands;

/// <summary>
/// Validação sintática (arquitetura/16-validacao-sintatica-vs-invariante.md). A autorização
/// (técnico atribuído, ou qualquer técnico da equipe se ainda Aberto) depende do estado do
/// agregado e mora no handler como guard explícito (ver ReclassificarChamadoCommand), não aqui.
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class ReclassificarChamadoCommandValidator : AbstractValidator<ReclassificarChamadoCommand>
{
    public ReclassificarChamadoCommandValidator()
    {
        RuleFor(c => c.ChamadoId).NotEmpty();
        RuleFor(c => c.NovaPrioridade).IsInEnum();
        RuleFor(c => c.IdempotencyKey).NotEmpty();
    }
}
