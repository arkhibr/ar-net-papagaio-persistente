using Chamados.Contracts;
using Chamados.Domain;
using Xunit;

namespace Chamados.Domain.UnitTests;

/// <summary>
/// Chamado.Abrir: prazo de SLA calculado a partir de "agora" + horasDeSla.
/// DateTimeOffset chega como parâmetro (nunca DateTime.Now dentro do Domain) —
/// ver plano-de-arquitetura.md secao 2 e arquitetura/13-estrategia-de-testes.md
/// ("Testabilidade de tempo").
///
/// horasDeSla é um snapshot resolvido pela Application via Catalogo.Contracts
/// (ResolverEquipeESlaQueryHandler, testado em Catalogo.Application.UnitTests) —
/// o Domain não mapeia prioridade -> horas, só aplica o valor recebido.
/// </summary>
public class ChamadoAbrirTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(72)]
    public void Abrir_deve_calcular_prazo_de_sla_a_partir_de_agora_mais_horasDeSla(int horasDeSla)
    {
        var solicitanteId = Guid.NewGuid();
        var categoriaId = Guid.NewGuid();
        var equipeId = Guid.NewGuid();

        var chamado = Chamado.Abrir(
            solicitanteId, categoriaId, equipeId, PrioridadeChamado.Media, horasDeSla, Agora);

        Assert.Equal(Agora, chamado.AbertoEm);
        Assert.Equal(Agora.AddHours(horasDeSla), chamado.PrazoSla);
    }

    [Fact]
    public void Abrir_deve_iniciar_no_estado_Aberto()
    {
        var chamado = Chamado.Abrir(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, Agora);

        Assert.Equal(StatusChamado.Aberto, chamado.Status);
    }

    [Fact]
    public void Abrir_deve_gravar_solicitante_categoria_e_equipe_como_snapshot()
    {
        var solicitanteId = Guid.NewGuid();
        var categoriaId = Guid.NewGuid();
        var equipeId = Guid.NewGuid();

        var chamado = Chamado.Abrir(
            solicitanteId, categoriaId, equipeId, PrioridadeChamado.Alta, horasDeSla: 8, Agora);

        Assert.Equal(solicitanteId, chamado.SolicitanteId);
        Assert.Equal(categoriaId, chamado.CategoriaId);
        Assert.Equal(equipeId, chamado.EquipeId);
    }

    [Fact]
    public void Abrir_nao_deve_iniciar_escalonado()
    {
        var chamado = Chamado.Abrir(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Baixa, horasDeSla: 72, Agora);

        Assert.False(chamado.Escalonado);
        Assert.Null(chamado.DataEscalonamento);
    }
}
