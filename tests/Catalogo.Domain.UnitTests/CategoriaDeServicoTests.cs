using Catalogo.Contracts;
using Catalogo.Domain;
using SharedKernel;
using Xunit;

namespace Catalogo.Domain.UnitTests;

/// <summary>
/// CategoriaDeServico com a equipe responsável e o SLA de cada prioridade
/// (especificacao-clarificada.md; M13 de achados.md: o SLA é dado de referência do Catálogo,
/// não regra fixa em código). Invariantes: equipe responsável obrigatória; no máximo um SLA
/// por prioridade (redefinir substitui); horas > 0.
/// </summary>
public class CategoriaDeServicoTests
{
    private static readonly (PrioridadeServico, int)[] SlasPadrao =
    [
        (PrioridadeServico.Critica, 4),
        (PrioridadeServico.Alta, 8),
        (PrioridadeServico.Media, 24),
        (PrioridadeServico.Baixa, 72),
    ];

    [Fact]
    public void Criar_com_equipe_valida_deve_criar_categoria_com_os_dados_informados()
    {
        var equipeId = Guid.NewGuid();

        var categoria = CategoriaDeServico.Criar("Suporte de Rede", equipeId, SlasPadrao);

        Assert.Equal("Suporte de Rede", categoria.Nome);
        Assert.Equal(equipeId, categoria.EquipeId);
        Assert.NotEqual(Guid.Empty, categoria.Id);
        Assert.Equal(4, categoria.Slas.Count);
    }

    [Fact]
    public void Criar_com_EquipeId_vazio_deve_lancar_DomainException()
    {
        var exception = Assert.Throws<DomainException>(
            () => CategoriaDeServico.Criar("Suporte de Rede", Guid.Empty, SlasPadrao));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Theory]
    [InlineData(PrioridadeServico.Critica, 4)]
    [InlineData(PrioridadeServico.Alta, 8)]
    [InlineData(PrioridadeServico.Media, 24)]
    [InlineData(PrioridadeServico.Baixa, 72)]
    public void HorasDeSlaPara_deve_devolver_o_SLA_definido_para_a_prioridade(PrioridadeServico prioridade, int horasEsperadas)
    {
        var categoria = CategoriaDeServico.Criar("Suporte de Rede", Guid.NewGuid(), SlasPadrao);

        Assert.Equal(horasEsperadas, categoria.HorasDeSlaPara(prioridade));
    }

    [Fact]
    public void HorasDeSlaPara_prioridade_sem_SLA_deve_devolver_nulo()
    {
        var categoria = CategoriaDeServico.Criar(
            "Suporte de Rede", Guid.NewGuid(), [(PrioridadeServico.Critica, 4)]);

        Assert.Null(categoria.HorasDeSlaPara(PrioridadeServico.Baixa));
    }

    [Fact]
    public void Criar_sem_nenhum_SLA_deve_ser_permitido()
    {
        var categoria = CategoriaDeServico.Criar("Suporte de Rede", Guid.NewGuid(), []);

        Assert.Empty(categoria.Slas);
    }

    [Fact]
    public void DefinirSla_para_prioridade_ja_definida_deve_substituir_o_valor_anterior()
    {
        var categoria = CategoriaDeServico.Criar("Suporte de Rede", Guid.NewGuid(), SlasPadrao);

        categoria.DefinirSla(PrioridadeServico.Critica, 2);

        Assert.Equal(2, categoria.HorasDeSlaPara(PrioridadeServico.Critica));
        Assert.Single(categoria.Slas, sla => sla.Prioridade == PrioridadeServico.Critica);
        Assert.Equal(4, categoria.Slas.Count);
    }

    [Fact]
    public void Criar_com_SLA_repetido_para_a_mesma_prioridade_deve_manter_so_o_ultimo()
    {
        var categoria = CategoriaDeServico.Criar(
            "Suporte de Rede", Guid.NewGuid(), [(PrioridadeServico.Media, 24), (PrioridadeServico.Media, 12)]);

        Assert.Equal(12, categoria.HorasDeSlaPara(PrioridadeServico.Media));
        Assert.Single(categoria.Slas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DefinirSla_com_horas_menor_ou_igual_a_zero_deve_lancar_DomainException(int horas)
    {
        var categoria = CategoriaDeServico.Criar("Suporte de Rede", Guid.NewGuid(), SlasPadrao);

        Assert.Throws<DomainException>(() => categoria.DefinirSla(PrioridadeServico.Alta, horas));

        Assert.Equal(8, categoria.HorasDeSlaPara(PrioridadeServico.Alta));
    }

    [Fact]
    public void Criar_com_SLA_de_zero_horas_deve_lancar_DomainException()
    {
        Assert.Throws<DomainException>(
            () => CategoriaDeServico.Criar("Suporte de Rede", Guid.NewGuid(), [(PrioridadeServico.Alta, 0)]));
    }
}
