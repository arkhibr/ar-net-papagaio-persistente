using Catalogo.Application.Queries;
using Catalogo.Application.UnitTests.Fakes;
using Catalogo.Contracts;
using Catalogo.Domain;
using Xunit;

namespace Catalogo.Application.UnitTests.Queries;

/// <summary>
/// ResolverEquipeESlaQueryHandler (modo TDD): implementação real da Query pública consumida
/// por Chamados (AbrirChamadoCommand/ReclassificarChamadoCommand) via Catalogo.Contracts.
/// Resolve EquipeId a partir da categoria e HorasDeSla a partir da prioridade
/// (Critica 4h, Alta 8h, Media 24h, Baixa 72h — especificacao-clarificada.md).
/// </summary>
public class ResolverEquipeESlaQueryHandlerTests
{
    [Theory]
    [InlineData(PrioridadeServico.Critica, 4)]
    [InlineData(PrioridadeServico.Alta, 8)]
    [InlineData(PrioridadeServico.Media, 24)]
    [InlineData(PrioridadeServico.Baixa, 72)]
    public async Task Deve_resolver_a_equipe_da_categoria_e_as_horas_de_sla_da_prioridade(
        PrioridadeServico prioridade, int horasEsperadas)
    {
        var equipeId = Guid.NewGuid();
        var categoria = CategoriaDeServico.Criar("Suporte de Rede", equipeId);
        var repository = new FakeCategoriaDeServicoRepository().ComCategoria(categoria);
        var handler = new ResolverEquipeESlaQueryHandler(repository);

        var resultado = await handler.Handle(
            new ResolverEquipeESlaQuery(categoria.Id, prioridade), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(equipeId, resultado.Value!.EquipeId);
        Assert.Equal(horasEsperadas, resultado.Value.HorasDeSla);
    }

    [Fact]
    public async Task Categoria_inexistente_deve_devolver_Result_Failure()
    {
        var repository = new FakeCategoriaDeServicoRepository();
        var handler = new ResolverEquipeESlaQueryHandler(repository);

        var resultado = await handler.Handle(
            new ResolverEquipeESlaQuery(Guid.NewGuid(), PrioridadeServico.Media), CancellationToken.None);

        Assert.True(resultado.IsFailure);
    }
}
