using Catalogo.Application.Queries;
using Catalogo.Application.UnitTests.Fakes;
using Catalogo.Contracts;
using Catalogo.Domain;
using Xunit;

namespace Catalogo.Application.UnitTests.Queries;

/// <summary>
/// ResolverEquipeESlaQueryHandler: snapshot de equipe e SLA consumido por Chamados ao
/// abrir/reclassificar (arquitetura/29 §1). O SLA vem da categoria no repositório (tabela
/// SlasDeCategoria, M13 de achados.md), não de um mapeamento fixo em código. Categoria
/// inexistente ou sem SLA para a prioridade é Result.Failure, nunca exceção.
/// </summary>
public class ResolverEquipeESlaQueryHandlerTests
{
    [Fact]
    public async Task Deve_resolver_a_equipe_e_as_horas_de_SLA_cadastradas_na_categoria()
    {
        var equipeId = Guid.NewGuid();
        // Valores diferentes da tabela da especificação: prova que o SLA vem do repositório.
        var categoria = CategoriaDeServico.Criar(
            "Suporte de Rede", equipeId, [(PrioridadeServico.Critica, 2), (PrioridadeServico.Baixa, 96)]);
        var handler = new ResolverEquipeESlaQueryHandler(new FakeCategoriaDeServicoRepository().ComCategoria(categoria));

        var critica = await handler.Handle(new ResolverEquipeESlaQuery(categoria.Id, PrioridadeServico.Critica), CancellationToken.None);
        var baixa = await handler.Handle(new ResolverEquipeESlaQuery(categoria.Id, PrioridadeServico.Baixa), CancellationToken.None);

        Assert.True(critica.IsSuccess);
        Assert.Equal(new ResolverEquipeESlaResultado(equipeId, 2), critica.Value);
        Assert.True(baixa.IsSuccess);
        Assert.Equal(new ResolverEquipeESlaResultado(equipeId, 96), baixa.Value);
    }

    [Fact]
    public async Task Categoria_sem_SLA_para_a_prioridade_deve_devolver_Result_Failure()
    {
        var categoria = CategoriaDeServico.Criar("Suporte de Rede", Guid.NewGuid(), [(PrioridadeServico.Critica, 4)]);
        var handler = new ResolverEquipeESlaQueryHandler(new FakeCategoriaDeServicoRepository().ComCategoria(categoria));

        var resultado = await handler.Handle(
            new ResolverEquipeESlaQuery(categoria.Id, PrioridadeServico.Media), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Contains("Media", resultado.Error);
    }

    [Fact]
    public async Task Categoria_inexistente_deve_devolver_Result_Failure()
    {
        var handler = new ResolverEquipeESlaQueryHandler(new FakeCategoriaDeServicoRepository());

        var resultado = await handler.Handle(
            new ResolverEquipeESlaQuery(Guid.NewGuid(), PrioridadeServico.Media), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal("Categoria de serviço não encontrada.", resultado.Error);
    }
}
