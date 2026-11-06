using Chamados.Contracts;
using FluentValidation;

namespace Chamados.Application.Commands;

/// <summary>
/// Validação sintática (arquitetura/16-validacao-sintatica-vs-invariante.md): a obrigatoriedade
/// de NotaResolucao é sintática (só depende do próprio campo, plano-de-arquitetura.md secao 2);
/// a transição em si ("Resolver só a partir de EmAtendimento") é invariante e continua só em
/// Chamado.Resolver — duplicação intencional entre validador e agregado (o agregado precisa
/// nascer válido para qualquer chamador, ex. job em lote/script; o validador agrega todas as
/// falhas de uma vez para melhor experiência de API).
/// internal: descoberto por DI dentro do próprio assembly (arquitetura/01).
/// </summary>
internal sealed class ResolverChamadoCommandValidator : AbstractValidator<ResolverChamadoCommand>
{
    public ResolverChamadoCommandValidator()
    {
        RuleFor(c => c.ChamadoId).NotEmpty();
        RuleFor(c => c.NotaResolucao).NotEmpty();
        RuleFor(c => c.IdempotencyKey).NotEmpty();
    }
}
