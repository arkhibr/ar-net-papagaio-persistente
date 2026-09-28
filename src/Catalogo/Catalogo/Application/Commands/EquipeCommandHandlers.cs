using Catalogo.Contracts;
using Catalogo.Domain;
using Mediator;
using SharedKernel;

namespace Catalogo.Application.Commands;

// Handlers da administração de equipes e membros (AdministracaoDoCatalogo.cs). O commit é do
// UnitOfWorkBehavior (ITransactionalCommand).

internal sealed class CriarEquipeCommandHandler : IRequestHandler<CriarEquipeCommand, Result<Guid>>
{
    private readonly IEquipeRepository _equipes;

    public CriarEquipeCommandHandler(IEquipeRepository equipes)
    {
        _equipes = equipes;
    }

    public async ValueTask<Result<Guid>> Handle(CriarEquipeCommand request, CancellationToken cancellationToken)
    {
        Equipe equipe;
        try
        {
            equipe = Equipe.Criar(Guid.NewGuid(), request.Nome);
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(ex.Message);
        }

        await _equipes.AdicionarAsync(equipe, cancellationToken);
        return Result<Guid>.Success(equipe.Id);
    }
}

internal sealed class RenomearEquipeCommandHandler : IRequestHandler<RenomearEquipeCommand, Result<Unit>>
{
    private readonly IEquipeRepository _equipes;

    public RenomearEquipeCommandHandler(IEquipeRepository equipes)
    {
        _equipes = equipes;
    }

    public async ValueTask<Result<Unit>> Handle(RenomearEquipeCommand request, CancellationToken cancellationToken)
    {
        var equipe = await _equipes.ObterParaEscritaAsync(request.EquipeId, cancellationToken);
        if (equipe is null)
        {
            return Result<Unit>.NotFound("Equipe não encontrada.");
        }

        try
        {
            equipe.Renomear(request.Nome);
        }
        catch (DomainException ex)
        {
            return Result<Unit>.Failure(ex.Message);
        }

        return Result<Unit>.Success(Unit.Value);
    }
}

/// <summary>
/// Um usuário pertence a no máximo uma equipe (decisão registrada em MembroDeEquipe): vincular a
/// outra equipe exige desvincular antes, para a troca ser explícita.
/// </summary>
internal sealed class VincularMembroDaEquipeCommandHandler : IRequestHandler<VincularMembroDaEquipeCommand, Result<Unit>>
{
    private readonly IEquipeRepository _equipes;
    private readonly IMembroDeEquipeRepository _membros;

    public VincularMembroDaEquipeCommandHandler(IEquipeRepository equipes, IMembroDeEquipeRepository membros)
    {
        _equipes = equipes;
        _membros = membros;
    }

    public async ValueTask<Result<Unit>> Handle(VincularMembroDaEquipeCommand request, CancellationToken cancellationToken)
    {
        if (!await _equipes.ExisteAsync(request.EquipeId, cancellationToken))
        {
            return Result<Unit>.NotFound("Equipe não encontrada.");
        }

        var equipeAtual = await _membros.ResolverEquipeIdAsync(request.UsuarioId, cancellationToken);
        if (equipeAtual == request.EquipeId)
        {
            return Result<Unit>.Success(Unit.Value);
        }

        if (equipeAtual is not null)
        {
            return Result<Unit>.Failure("O usuário já pertence a outra equipe. Desvincule-o de lá antes.");
        }

        await _membros.VincularAsync(request.UsuarioId, request.EquipeId, cancellationToken);
        return Result<Unit>.Success(Unit.Value);
    }
}

internal sealed class DesvincularMembroDaEquipeCommandHandler : IRequestHandler<DesvincularMembroDaEquipeCommand, Result<Unit>>
{
    private readonly IMembroDeEquipeRepository _membros;

    public DesvincularMembroDaEquipeCommandHandler(IMembroDeEquipeRepository membros)
    {
        _membros = membros;
    }

    public async ValueTask<Result<Unit>> Handle(DesvincularMembroDaEquipeCommand request, CancellationToken cancellationToken) =>
        await _membros.DesvincularAsync(request.UsuarioId, request.EquipeId, cancellationToken)
            ? Result<Unit>.Success(Unit.Value)
            : Result<Unit>.NotFound("O usuário não é membro desta equipe.");
}
