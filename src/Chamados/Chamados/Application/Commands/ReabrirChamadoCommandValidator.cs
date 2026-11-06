using Chamados.Contracts;
using FluentValidation;

namespace Chamados.Application.Commands;

/// <summary>
/// Validação sintática (arquitetura/16-validacao-sintatica-vs-invariante.md). A janela de 5
/// dias corridos do fechamento depende do estado do agregado (FechadoEm) e continua invariante
/// em Chamado.Reabrir, não aqui.
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class ReabrirChamadoCommandValidator : AbstractValidator<ReabrirChamadoCommand>
{
    public ReabrirChamadoCommandValidator()
    {
        RuleFor(c => c.ChamadoId).NotEmpty();
        RuleFor(c => c.IdempotencyKey).NotEmpty();
    }
}
