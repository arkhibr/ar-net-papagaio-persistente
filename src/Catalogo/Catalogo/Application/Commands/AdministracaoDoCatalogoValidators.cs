using Catalogo.Contracts;
using Catalogo.Domain;
using FluentValidation;

namespace Catalogo.Application.Commands;

// Validação sintática (arquitetura/16): só o valor de cada campo. As invariantes (nome, SLA
// positivo, prioridade única) continuam também no agregado, para valer para qualquer chamador;
// aqui elas viram 400 com errors[] e pointer por campo.

internal sealed class CriarCategoriaDeServicoCommandValidator : AbstractValidator<CriarCategoriaDeServicoCommand>
{
    public CriarCategoriaDeServicoCommandValidator()
    {
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(CategoriaDeServico.TamanhoMaximoDoNome);
        RuleFor(c => c.EquipeId).NotEmpty();
        RuleFor(c => c.Slas).NotNull().SetValidator(new SlasValidator());
    }
}

internal sealed class RenomearCategoriaDeServicoCommandValidator : AbstractValidator<RenomearCategoriaDeServicoCommand>
{
    public RenomearCategoriaDeServicoCommandValidator()
    {
        RuleFor(c => c.CategoriaId).NotEmpty();
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(CategoriaDeServico.TamanhoMaximoDoNome);
    }
}

internal sealed class TransferirCategoriaDeServicoCommandValidator : AbstractValidator<TransferirCategoriaDeServicoCommand>
{
    public TransferirCategoriaDeServicoCommandValidator()
    {
        RuleFor(c => c.CategoriaId).NotEmpty();
        RuleFor(c => c.EquipeId).NotEmpty();
    }
}

internal sealed class DefinirSlasDaCategoriaCommandValidator : AbstractValidator<DefinirSlasDaCategoriaCommand>
{
    public DefinirSlasDaCategoriaCommandValidator()
    {
        RuleFor(c => c.CategoriaId).NotEmpty();
        RuleFor(c => c.Slas).NotNull().SetValidator(new SlasValidator());
    }
}

internal sealed class CriarEquipeCommandValidator : AbstractValidator<CriarEquipeCommand>
{
    public CriarEquipeCommandValidator()
    {
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(Equipe.TamanhoMaximoDoNome);
    }
}

internal sealed class RenomearEquipeCommandValidator : AbstractValidator<RenomearEquipeCommand>
{
    public RenomearEquipeCommandValidator()
    {
        RuleFor(c => c.EquipeId).NotEmpty();
        RuleFor(c => c.Nome).NotEmpty().MaximumLength(Equipe.TamanhoMaximoDoNome);
    }
}

internal sealed class VincularMembroDaEquipeCommandValidator : AbstractValidator<VincularMembroDaEquipeCommand>
{
    public VincularMembroDaEquipeCommandValidator()
    {
        RuleFor(c => c.EquipeId).NotEmpty();
        RuleFor(c => c.UsuarioId).NotEmpty();
    }
}

internal sealed class SlasValidator : AbstractValidator<IReadOnlyList<SlaDto>>
{
    public SlasValidator()
    {
        RuleForEach(slas => slas).ChildRules(sla =>
        {
            sla.RuleFor(s => s.Prioridade).IsInEnum();
            sla.RuleFor(s => s.Horas).GreaterThan(0);
        });
        RuleFor(slas => slas)
            .Must(slas => slas.Select(s => s.Prioridade).Distinct().Count() == slas.Count)
            .WithErrorCode("prioridade_repetida")
            .WithMessage("Cada prioridade pode aparecer uma única vez.");
    }
}
