using Chamados.Contracts;
using Chamados.Domain;
using SharedKernel;
using Xunit;

namespace Chamados.Domain.UnitTests;

/// <summary>
/// Chamado.Reclassificar: recalcula o prazo de SLA a partir da abertura original
/// (AbertoEm), usando o horasDeSla recebido como snapshot da nova prioridade —
/// nunca a partir de "agora" (especificacao-clarificada.md: "Efeito da
/// reclassificação no prazo"). Válido em Aberto ou EmAtendimento
/// (plano-de-arquitetura.md, secao 2).
///
/// horasDeSla é resolvido pela Application via Catalogo.Contracts (o Domain não
/// mapeia prioridade -> horas; só aplica o valor recebido) — o mapeamento em si
/// é testado em Catalogo.Application.UnitTests/Queries/ResolverEquipeESlaQueryHandlerTests.
/// </summary>
public class ChamadoReclassificarTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    private static Chamado NovoChamado(
        PrioridadeChamado prioridadeInicial = PrioridadeChamado.Media, int horasDeSlaInicial = 24) =>
        Chamado.Abrir(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), prioridadeInicial, horasDeSlaInicial, AbertoEm);

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(72)]
    public void Reclassificar_deve_recalcular_o_prazo_de_sla_a_partir_da_abertura_original(int horasDeSla)
    {
        var chamado = NovoChamado(prioridadeInicial: PrioridadeChamado.Baixa, horasDeSlaInicial: 72);

        chamado.Reclassificar(PrioridadeChamado.Critica, horasDeSla);

        Assert.Equal(PrioridadeChamado.Critica, chamado.Prioridade);
        Assert.Equal(AbertoEm.AddHours(horasDeSla), chamado.PrazoSla);
    }

    [Fact]
    public void Reclassificar_nao_deve_alterar_a_data_de_abertura_original()
    {
        var chamado = NovoChamado(prioridadeInicial: PrioridadeChamado.Baixa, horasDeSlaInicial: 72);

        chamado.Reclassificar(PrioridadeChamado.Critica, horasDeSla: 4);

        Assert.Equal(AbertoEm, chamado.AbertoEm);
    }

    [Fact]
    public void Reclassificar_deve_ser_permitido_quando_o_chamado_esta_Aberto()
    {
        var chamado = NovoChamado();

        var exception = Record.Exception(() => chamado.Reclassificar(PrioridadeChamado.Alta, horasDeSla: 8));

        Assert.Null(exception);
    }

    [Fact]
    public void Reclassificar_deve_ser_permitido_quando_o_chamado_esta_EmAtendimento()
    {
        var chamado = NovoChamado();
        chamado.Atribuir(Guid.NewGuid());

        var exception = Record.Exception(() => chamado.Reclassificar(PrioridadeChamado.Alta, horasDeSla: 8));

        Assert.Null(exception);
    }

    [Fact]
    public void Reclassificar_deve_lancar_DomainException_quando_o_chamado_esta_Resolvido()
    {
        var chamado = NovoChamado();
        chamado.Atribuir(Guid.NewGuid());
        chamado.Resolver("Reiniciei o serviço.", AbertoEm.AddHours(1));

        Assert.Throws<DomainException>(() => chamado.Reclassificar(PrioridadeChamado.Alta, horasDeSla: 8));
    }

    [Fact]
    public void Reclassificar_deve_lancar_DomainException_quando_o_chamado_esta_Fechado()
    {
        var chamado = NovoChamado();
        chamado.Atribuir(Guid.NewGuid());
        chamado.Resolver("Reiniciei o serviço.", AbertoEm.AddHours(1));
        chamado.Fechar(AbertoEm.AddHours(2));

        Assert.Throws<DomainException>(() => chamado.Reclassificar(PrioridadeChamado.Alta, horasDeSla: 8));
    }
}
