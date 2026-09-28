using Catalogo.Contracts;
using Catalogo.Domain;
using Mediator;
using SharedKernel;

namespace Catalogo.Application.Commands;

// Handlers da administração de categorias (AdministracaoDoCatalogo.cs). Todos seguem o mesmo
// molde: carregar o agregado, chamar a ação nomeada, DomainException → Result.Failure, e
// invalidar a lista em cache (CategoriasDeServicoQuery). O commit é do UnitOfWorkBehavior
// (ITransactionalCommand). A invalidação acontece antes do commit: uma leitura concorrente
// nesse intervalo pode recolocar a lista antiga no cache, que expira no TTL (60 s).

internal sealed class CriarCategoriaDeServicoCommandHandler : IRequestHandler<CriarCategoriaDeServicoCommand, Result<Guid>>
{
    private readonly ICategoriaDeServicoRepository _categorias;
    private readonly IEquipeRepository _equipes;
    private readonly ICacheInvalidator _cache;

    public CriarCategoriaDeServicoCommandHandler(
        ICategoriaDeServicoRepository categorias, IEquipeRepository equipes, ICacheInvalidator cache)
    {
        _categorias = categorias;
        _equipes = equipes;
        _cache = cache;
    }

    public async ValueTask<Result<Guid>> Handle(CriarCategoriaDeServicoCommand request, CancellationToken cancellationToken)
    {
        if (!await _equipes.ExisteAsync(request.EquipeId, cancellationToken))
        {
            return Result<Guid>.Failure("Equipe responsável não encontrada.");
        }

        CategoriaDeServico categoria;
        try
        {
            categoria = CategoriaDeServico.Criar(Guid.NewGuid(), request.Nome, request.EquipeId, []);
            categoria.RedefinirSlas(request.Slas.Select(s => (s.Prioridade, s.Horas)));
        }
        catch (DomainException ex)
        {
            return Result<Guid>.Failure(ex.Message);
        }

        await _categorias.AdicionarAsync(categoria, cancellationToken);
        CacheDeCategorias.Invalidar(_cache);
        return Result<Guid>.Success(categoria.Id);
    }
}

internal sealed class RenomearCategoriaDeServicoCommandHandler : IRequestHandler<RenomearCategoriaDeServicoCommand, Result<Unit>>
{
    private readonly AcaoSobreCategoria _acao;

    public RenomearCategoriaDeServicoCommandHandler(ICategoriaDeServicoRepository categorias, ICacheInvalidator cache)
    {
        _acao = new AcaoSobreCategoria(categorias, cache);
    }

    public ValueTask<Result<Unit>> Handle(RenomearCategoriaDeServicoCommand request, CancellationToken cancellationToken) =>
        _acao.ExecutarAsync(request.CategoriaId, c => c.Renomear(request.Nome), cancellationToken);
}

internal sealed class TransferirCategoriaDeServicoCommandHandler : IRequestHandler<TransferirCategoriaDeServicoCommand, Result<Unit>>
{
    private readonly AcaoSobreCategoria _acao;
    private readonly IEquipeRepository _equipes;

    public TransferirCategoriaDeServicoCommandHandler(
        ICategoriaDeServicoRepository categorias, IEquipeRepository equipes, ICacheInvalidator cache)
    {
        _acao = new AcaoSobreCategoria(categorias, cache);
        _equipes = equipes;
    }

    public async ValueTask<Result<Unit>> Handle(TransferirCategoriaDeServicoCommand request, CancellationToken cancellationToken)
    {
        if (!await _equipes.ExisteAsync(request.EquipeId, cancellationToken))
        {
            return Result<Unit>.Failure("Equipe responsável não encontrada.");
        }

        return await _acao.ExecutarAsync(request.CategoriaId, c => c.TransferirPara(request.EquipeId), cancellationToken);
    }
}

internal sealed class DefinirSlasDaCategoriaCommandHandler : IRequestHandler<DefinirSlasDaCategoriaCommand, Result<Unit>>
{
    private readonly AcaoSobreCategoria _acao;

    public DefinirSlasDaCategoriaCommandHandler(ICategoriaDeServicoRepository categorias, ICacheInvalidator cache)
    {
        _acao = new AcaoSobreCategoria(categorias, cache);
    }

    public ValueTask<Result<Unit>> Handle(DefinirSlasDaCategoriaCommand request, CancellationToken cancellationToken) =>
        _acao.ExecutarAsync(
            request.CategoriaId, c => c.RedefinirSlas(request.Slas.Select(s => (s.Prioridade, s.Horas))), cancellationToken);
}

internal sealed class InativarCategoriaDeServicoCommandHandler : IRequestHandler<InativarCategoriaDeServicoCommand, Result<Unit>>
{
    private readonly AcaoSobreCategoria _acao;

    public InativarCategoriaDeServicoCommandHandler(ICategoriaDeServicoRepository categorias, ICacheInvalidator cache)
    {
        _acao = new AcaoSobreCategoria(categorias, cache);
    }

    public ValueTask<Result<Unit>> Handle(InativarCategoriaDeServicoCommand request, CancellationToken cancellationToken) =>
        _acao.ExecutarAsync(request.CategoriaId, c => c.Inativar(), cancellationToken);
}

internal sealed class ReativarCategoriaDeServicoCommandHandler : IRequestHandler<ReativarCategoriaDeServicoCommand, Result<Unit>>
{
    private readonly AcaoSobreCategoria _acao;

    public ReativarCategoriaDeServicoCommandHandler(ICategoriaDeServicoRepository categorias, ICacheInvalidator cache)
    {
        _acao = new AcaoSobreCategoria(categorias, cache);
    }

    public ValueTask<Result<Unit>> Handle(ReativarCategoriaDeServicoCommand request, CancellationToken cancellationToken) =>
        _acao.ExecutarAsync(request.CategoriaId, c => c.Reativar(), cancellationToken);
}

/// <summary>Carrega, aplica a ação, traduz DomainException e invalida o cache da lista.</summary>
internal sealed class AcaoSobreCategoria
{
    private readonly ICategoriaDeServicoRepository _categorias;
    private readonly ICacheInvalidator _cache;

    public AcaoSobreCategoria(ICategoriaDeServicoRepository categorias, ICacheInvalidator cache)
    {
        _categorias = categorias;
        _cache = cache;
    }

    public async ValueTask<Result<Unit>> ExecutarAsync(
        Guid categoriaId, Action<CategoriaDeServico> acao, CancellationToken cancellationToken)
    {
        var categoria = await _categorias.ObterParaEscritaAsync(categoriaId, cancellationToken);
        if (categoria is null)
        {
            return Result<Unit>.NotFound("Categoria de serviço não encontrada.");
        }

        try
        {
            acao(categoria);
        }
        catch (DomainException ex)
        {
            return Result<Unit>.Failure(ex.Message);
        }

        CacheDeCategorias.Invalidar(_cache);
        return Result<Unit>.Success(Unit.Value);
    }
}

internal static class CacheDeCategorias
{
    public static void Invalidar(ICacheInvalidator cache)
    {
        cache.InvalidarGlobal<CategoriasDeServicoQuery>(CategoriasDeServicoQuery.CacheKeyAtivas);
        cache.InvalidarGlobal<CategoriasDeServicoQuery>(CategoriasDeServicoQuery.CacheKeyTodas);
    }
}
