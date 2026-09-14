using Chamados.Contracts;
using Chamados.Domain;
using SharedKernel;
using Xunit;

namespace Chamados.Domain.UnitTests;

/// <summary>
/// Chamado.Escalonar: marca o chamado como escalonado (flag + data) no MESMO estado
/// atual — Escalonado não é um estado novo, é informativo (especificacao-clarificada.md,
/// "Pós-escalonamento"; plano-de-arquitetura.md, secao 2). Só válido em Aberto/EmAtendimento;
/// rejeita em Resolvido/Fechado (o chamado já deixou de precisar de acompanhamento de SLA).
/// </summary>
public class ChamadoEscalonarTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    private static Chamado NovoChamado() =>
        Chamado.Abrir(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Critica, horasDeSla: 4, AbertoEm);

    [Fact]
    public void Escalonar_a_partir_de_Aberto_deve_marcar_a_flag_e_a_data_sem_mudar_o_status()
    {
        var chamado = NovoChamado();
        var dataEscalonamento = AbertoEm.AddHours(5);

        chamado.Escalonar(dataEscalonamento);

        Assert.True(chamado.Escalonado);
        Assert.Equal(dataEscalonamento, chamado.DataEscalonamento);
        Assert.Equal(StatusChamado.Aberto, chamado.Status);
    }

    [Fact]
    public void Escalonar_a_partir_de_EmAtendimento_deve_marcar_a_flag_e_a_data_sem_mudar_o_status()
    {
        var chamado = NovoChamado();
        chamado.Atribuir(Guid.NewGuid());
        var dataEscalonamento = AbertoEm.AddHours(9);

        chamado.Escalonar(dataEscalonamento);

        Assert.True(chamado.Escalonado);
        Assert.Equal(dataEscalonamento, chamado.DataEscalonamento);
        Assert.Equal(StatusChamado.EmAtendimento, chamado.Status);
    }

    [Fact]
    public void Escalonar_a_partir_de_Resolvido_deve_lancar_DomainException()
    {
        var chamado = NovoChamado();
        chamado.Atribuir(Guid.NewGuid());
        chamado.Resolver("Problema resolvido.", AbertoEm.AddHours(1));

        Assert.Throws<DomainException>(() => chamado.Escalonar(AbertoEm.AddHours(2)));
    }

    [Fact]
    public void Escalonar_a_partir_de_Fechado_deve_lancar_DomainException()
    {
        var chamado = NovoChamado();
        chamado.Atribuir(Guid.NewGuid());
        chamado.Resolver("Problema resolvido.", AbertoEm.AddHours(1));
        chamado.Fechar(AbertoEm.AddHours(2));

        Assert.Throws<DomainException>(() => chamado.Escalonar(AbertoEm.AddHours(3)));
    }

    [Fact]
    public void Escalonar_nao_deve_alterar_o_prazo_de_sla()
    {
        var chamado = NovoChamado();
        var prazoOriginal = chamado.PrazoSla;

        chamado.Escalonar(AbertoEm.AddHours(5));

        Assert.Equal(prazoOriginal, chamado.PrazoSla);
    }
}
