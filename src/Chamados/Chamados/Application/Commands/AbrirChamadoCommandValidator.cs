using Chamados.Contracts;
using FluentValidation;

namespace Chamados.Application.Commands;

/// <summary>
/// Validação sintática (arquitetura/16-validacao-sintatica-vs-invariante.md): depende só do
/// valor de cada campo, isolado, sem consultar estado de agregado ou outro dado de contexto.
/// Executado pelo ValidationBehavior antes do handler (-> 400 via ValidationException se falhar).
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class AbrirChamadoCommandValidator : AbstractValidator<AbrirChamadoCommand>
{
    public AbrirChamadoCommandValidator()
    {
        RuleFor(c => c.SolicitanteId).NotEmpty();
        RuleFor(c => c.CategoriaId).NotEmpty();
        RuleFor(c => c.Prioridade).IsInEnum();
        RuleFor(c => c.IdempotencyKey).NotEmpty();
    }
}
