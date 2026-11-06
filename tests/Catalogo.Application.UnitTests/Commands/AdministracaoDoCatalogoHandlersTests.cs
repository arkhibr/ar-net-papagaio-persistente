using Catalogo.Application.Commands;
using Catalogo.Application.UnitTests.Fakes;
using Catalogo.Contracts;
using Catalogo.Domain;
using SharedKernel;
using Xunit;

namespace Catalogo.Application.UnitTests.Commands;

/// <summary>Handlers da administração do Catálogo: referências de equipe, vínculo único e cache.</summary>
public class AdministracaoDoCatalogoHandlersTests
{
    private readonly Equipe _equipe = Equipe.Criar(Guid.NewGuid(), "Infra");
    private readonly Equipe _outraEquipe = Equipe.Criar(Guid.NewGuid(), "Suporte");

    [Fact]
    public async Task Criar_categoria_com_equipe_inexistente_e_falha_de_negocio()
    {
        var handler = new CriarCategoriaDeServicoCommandHandler(
            new FakeCategoriaDeServicoRepository(), new FakeEquipeRepository(), new FakeCacheInvalidator());

        var resultado = await handler.Handle(
            new CriarCategoriaDeServicoCommand("Rede", Guid.NewGuid(), [new SlaDto(PrioridadeServico.Media, 24)]), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorKind.BusinessRule, resultado.ErrorKind);
    }

    [Fact]
    public async Task Criar_categoria_grava_os_slas_e_invalida_as_duas_listas_em_cache()
    {
        var categorias = new FakeCategoriaDeServicoRepository();
        var cache = new FakeCacheInvalidator();
        var handler = new CriarCategoriaDeServicoCommandHandler(categorias, new FakeEquipeRepository().ComEquipe(_equipe), cache);

        var resultado = await handler.Handle(
            new CriarCategoriaDeServicoCommand("Rede", _equipe.Id, [new SlaDto(PrioridadeServico.Alta, 8)]), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var criada = await categorias.ObterParaEscritaAsync(resultado.Value, CancellationToken.None);
        Assert.Equal(8, criada!.HorasDeSlaPara(PrioridadeServico.Alta));
        Assert.Equal([CategoriasDeServicoQuery.CacheKeyAtivas, CategoriasDeServicoQuery.CacheKeyTodas], cache.Invalidadas);
    }

    [Fact]
    public async Task Acao_sobre_categoria_inexistente_e_not_found()
    {
        var handler = new InativarCategoriaDeServicoCommandHandler(new FakeCategoriaDeServicoRepository(), new FakeCacheInvalidator());

        var resultado = await handler.Handle(new InativarCategoriaDeServicoCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErrorKind.NotFound, resultado.ErrorKind);
    }

    [Fact]
    public async Task Vincular_usuario_que_ja_esta_em_outra_equipe_e_falha_de_negocio()
    {
        var usuario = Guid.NewGuid();
        var membros = new FakeMembroDeEquipeRepository().ComVinculo(usuario, _outraEquipe.Id);
        var handler = new VincularMembroDaEquipeCommandHandler(new FakeEquipeRepository().ComEquipe(_equipe).ComEquipe(_outraEquipe), membros);

        var resultado = await handler.Handle(new VincularMembroDaEquipeCommand(_equipe.Id, usuario), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(_outraEquipe.Id, await membros.ResolverEquipeIdAsync(usuario, CancellationToken.None));
    }

    [Fact]
    public async Task Vincular_de_novo_a_mesma_equipe_nao_muda_nada()
    {
        var usuario = Guid.NewGuid();
        var membros = new FakeMembroDeEquipeRepository().ComVinculo(usuario, _equipe.Id);
        var handler = new VincularMembroDaEquipeCommandHandler(new FakeEquipeRepository().ComEquipe(_equipe), membros);

        var resultado = await handler.Handle(new VincularMembroDaEquipeCommand(_equipe.Id, usuario), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Desvincular_quem_nao_e_membro_e_not_found()
    {
        var handler = new DesvincularMembroDaEquipeCommandHandler(new FakeMembroDeEquipeRepository());

        var resultado = await handler.Handle(new DesvincularMembroDaEquipeCommand(_equipe.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErrorKind.NotFound, resultado.ErrorKind);
    }
}
