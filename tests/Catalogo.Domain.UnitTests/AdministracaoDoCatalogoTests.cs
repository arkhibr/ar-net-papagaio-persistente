using Catalogo.Contracts;
using Catalogo.Domain;
using SharedKernel;
using Xunit;

namespace Catalogo.Domain.UnitTests;

/// <summary>Ações de administração: Equipe e as ações nomeadas de CategoriaDeServico.</summary>
public class AdministracaoDoCatalogoTests
{
    private static CategoriaDeServico NovaCategoria() =>
        CategoriaDeServico.Criar(Guid.NewGuid(), "Rede", Guid.NewGuid(), [(PrioridadeServico.Media, 24)]);

    [Fact]
    public void Equipe_criada_com_nome_valido_guarda_id_e_nome_sem_espacos()
    {
        var id = Guid.NewGuid();

        var equipe = Equipe.Criar(id, "  Infraestrutura  ");

        Assert.Equal(id, equipe.Id);
        Assert.Equal("Infraestrutura", equipe.Nome);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Equipe_sem_nome_e_invalida(string nome)
    {
        Assert.Throws<DomainException>(() => Equipe.Criar(Guid.NewGuid(), nome));
        Assert.Throws<DomainException>(() => Equipe.Criar(Guid.NewGuid(), "ok").Renomear(nome));
    }

    [Fact]
    public void Equipe_com_nome_acima_do_limite_e_invalida()
    {
        Assert.Throws<DomainException>(() => Equipe.Criar(Guid.NewGuid(), new string('x', Equipe.TamanhoMaximoDoNome + 1)));
    }

    [Fact]
    public void Categoria_nasce_ativa_e_inativar_reativar_sao_idempotentes()
    {
        var categoria = NovaCategoria();
        Assert.True(categoria.Ativa);

        categoria.Inativar();
        categoria.Inativar();
        Assert.False(categoria.Ativa);

        categoria.Reativar();
        categoria.Reativar();
        Assert.True(categoria.Ativa);
    }

    [Fact]
    public void Renomear_e_transferir_validam_nome_e_equipe()
    {
        var categoria = NovaCategoria();
        var novaEquipe = Guid.NewGuid();

        categoria.Renomear(" Rede corporativa ");
        categoria.TransferirPara(novaEquipe);

        Assert.Equal("Rede corporativa", categoria.Nome);
        Assert.Equal(novaEquipe, categoria.EquipeId);
        Assert.Throws<DomainException>(() => categoria.Renomear(""));
        Assert.Throws<DomainException>(() => categoria.TransferirPara(Guid.Empty));
    }

    [Fact]
    public void RedefinirSlas_substitui_a_tabela_inteira()
    {
        var categoria = NovaCategoria();

        categoria.RedefinirSlas([(PrioridadeServico.Alta, 8), (PrioridadeServico.Critica, 2)]);

        Assert.Null(categoria.HorasDeSlaPara(PrioridadeServico.Media));
        Assert.Equal(8, categoria.HorasDeSlaPara(PrioridadeServico.Alta));
        Assert.Equal(2, categoria.HorasDeSlaPara(PrioridadeServico.Critica));
    }

    [Fact]
    public void RedefinirSlas_recusa_prioridade_repetida_ou_horas_nao_positivas_sem_mudar_a_tabela()
    {
        var categoria = NovaCategoria();

        Assert.Throws<DomainException>(() => categoria.RedefinirSlas([(PrioridadeServico.Alta, 8), (PrioridadeServico.Alta, 4)]));
        Assert.Throws<DomainException>(() => categoria.RedefinirSlas([(PrioridadeServico.Alta, 0)]));

        Assert.Equal(24, categoria.HorasDeSlaPara(PrioridadeServico.Media));
    }
}
