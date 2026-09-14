using Catalogo.Domain;
using SharedKernel;
using Xunit;

namespace Catalogo.Domain.UnitTests;

/// <summary>
/// CategoriaDeServico é majoritariamente anêmico (plano-de-arquitetura.md, secao 2;
/// especificacao-clarificada.md). Único invariante: toda categoria precisa de equipe
/// responsável definida na criação.
/// </summary>
public class CategoriaDeServicoTests
{
    [Fact]
    public void Criar_com_equipe_valida_deve_criar_categoria_com_os_dados_informados()
    {
        var equipeId = Guid.NewGuid();

        var categoria = CategoriaDeServico.Criar("Suporte de Rede", equipeId);

        Assert.Equal("Suporte de Rede", categoria.Nome);
        Assert.Equal(equipeId, categoria.EquipeId);
        Assert.NotEqual(Guid.Empty, categoria.Id);
    }

    [Fact]
    public void Criar_com_EquipeId_vazio_deve_lancar_DomainException()
    {
        var exception = Assert.Throws<DomainException>(
            () => CategoriaDeServico.Criar("Suporte de Rede", Guid.Empty));

        Assert.NotNull(exception.Message);
        Assert.NotEqual(string.Empty, exception.Message);
    }
}
