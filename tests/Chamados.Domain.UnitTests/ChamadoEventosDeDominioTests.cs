using Chamados.Contracts;
using Chamados.Domain;
using SharedKernel;
using Xunit;

namespace Chamados.Domain.UnitTests;

/// <summary>
/// Eventos de domínio auditáveis do Chamado (arquitetura/21-auditoria.md, "Marcação do evento
/// auditável"): abertura, atribuição, resolução e escalonamento levantam evento IAuditable com
/// ValorAnterior/ValorNovo/Motivo na linguagem do evento; Devolver, Reclassificar, Fechar e
/// Reabrir não levantam (fora do subconjunto auditado da especificação). Quem fez a ação não
/// está no evento: é resolvido por ICurrentUser na materialização.
/// </summary>
public class ChamadoEventosDeDominioTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    private static Chamado NovoChamadoSemEventos()
    {
        var chamado = Chamado.Abrir(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        chamado.ClearDomainEvents();
        return chamado;
    }

    private static void AssertEventoDoChamado(IAuditable evento, Chamado chamado, string acao)
    {
        Assert.Equal(acao, evento.Acao);
        Assert.Equal("Chamado", evento.TipoRecurso);
        Assert.Equal(chamado.Id, evento.RecursoId);
    }

    [Fact]
    public void Abrir_deve_levantar_ChamadoAberto_com_status_prioridade_e_prazo()
    {
        var chamado = Chamado.Abrir(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Alta, horasDeSla: 8, AbertoEm);

        var evento = Assert.IsType<ChamadoAberto>(Assert.Single(chamado.DomainEvents));
        AssertEventoDoChamado(evento, chamado, "ChamadoAberto");
        Assert.Null(evento.ValorAnterior);
        Assert.Equal($"Status=Aberto;Prioridade=Alta;PrazoSla={AbertoEm.AddHours(8):O}", evento.ValorNovo);
        Assert.Null(evento.Motivo);
    }

    [Fact]
    public void Atribuir_deve_levantar_ChamadoAtribuido_com_o_tecnico_no_ValorNovo()
    {
        var chamado = NovoChamadoSemEventos();
        var tecnicoId = Guid.NewGuid();

        chamado.Atribuir(tecnicoId);

        var evento = Assert.IsType<ChamadoAtribuido>(Assert.Single(chamado.DomainEvents));
        AssertEventoDoChamado(evento, chamado, "ChamadoAtribuido");
        Assert.Equal("Status=Aberto", evento.ValorAnterior);
        Assert.Equal($"Status=EmAtendimento;TecnicoAtribuidoId={tecnicoId}", evento.ValorNovo);
        Assert.Null(evento.Motivo);
    }

    [Fact]
    public void Resolver_deve_levantar_ChamadoResolvido_com_a_nota_como_Motivo()
    {
        var chamado = NovoChamadoSemEventos();
        chamado.Atribuir(Guid.NewGuid());
        chamado.ClearDomainEvents();

        chamado.Resolver("Reiniciei o serviço.", AbertoEm.AddHours(2));

        var evento = Assert.IsType<ChamadoResolvido>(Assert.Single(chamado.DomainEvents));
        AssertEventoDoChamado(evento, chamado, "ChamadoResolvido");
        Assert.Equal("Status=EmAtendimento", evento.ValorAnterior);
        Assert.Equal("Status=Resolvido", evento.ValorNovo);
        Assert.Equal("Reiniciei o serviço.", evento.Motivo);
    }

    [Fact]
    public void Primeiro_escalonamento_deve_levantar_ChamadoEscalonado_partindo_de_nao_escalonado()
    {
        var chamado = NovoChamadoSemEventos();
        var escalonadoEm = AbertoEm.AddHours(25);

        chamado.Escalonar(escalonadoEm);

        var evento = Assert.IsType<ChamadoEscalonado>(Assert.Single(chamado.DomainEvents));
        AssertEventoDoChamado(evento, chamado, "ChamadoEscalonado");
        Assert.Equal("Escalonado=False", evento.ValorAnterior);
        Assert.Equal($"Escalonado=True;DataEscalonamento={escalonadoEm:O}", evento.ValorNovo);
        Assert.Null(evento.Motivo);
    }

    [Fact]
    public void Novo_escalonamento_deve_registrar_a_data_anterior_no_ValorAnterior()
    {
        var chamado = NovoChamadoSemEventos();
        var primeiro = AbertoEm.AddHours(25);
        var segundo = AbertoEm.AddHours(30);
        chamado.Escalonar(primeiro);
        chamado.ClearDomainEvents();

        chamado.Escalonar(segundo);

        var evento = Assert.IsType<ChamadoEscalonado>(Assert.Single(chamado.DomainEvents));
        Assert.Equal($"Escalonado=True;DataEscalonamento={primeiro:O}", evento.ValorAnterior);
        Assert.Equal($"Escalonado=True;DataEscalonamento={segundo:O}", evento.ValorNovo);
    }

    [Fact]
    public void Devolver_nao_deve_levantar_evento()
    {
        var chamado = NovoChamadoSemEventos();
        chamado.Atribuir(Guid.NewGuid());
        chamado.ClearDomainEvents();

        chamado.Devolver();

        Assert.Empty(chamado.DomainEvents);
    }

    [Fact]
    public void Reclassificar_nao_deve_levantar_evento()
    {
        var chamado = NovoChamadoSemEventos();

        chamado.Reclassificar(PrioridadeChamado.Critica, horasDeSla: 4);

        Assert.Empty(chamado.DomainEvents);
    }

    [Fact]
    public void Fechar_e_Reabrir_nao_devem_levantar_evento()
    {
        var chamado = NovoChamadoSemEventos();
        chamado.Atribuir(Guid.NewGuid());
        chamado.Resolver("Resolvido.", AbertoEm.AddHours(1));
        chamado.ClearDomainEvents();

        chamado.Fechar(AbertoEm.AddHours(2));
        Assert.Empty(chamado.DomainEvents);

        chamado.Reabrir(AbertoEm.AddDays(1));
        Assert.Empty(chamado.DomainEvents);
    }

    [Fact]
    public void Transicao_invalida_nao_deve_levantar_evento()
    {
        var chamado = NovoChamadoSemEventos();

        Assert.Throws<DomainException>(() => chamado.Resolver("Nota.", AbertoEm.AddHours(1)));

        Assert.Empty(chamado.DomainEvents);
    }

    [Fact]
    public void Eventos_devem_se_acumular_na_ordem_ate_ClearDomainEvents()
    {
        var chamado = Chamado.Abrir(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        chamado.Atribuir(Guid.NewGuid());

        Assert.Collection(
            chamado.DomainEvents,
            e => Assert.IsType<ChamadoAberto>(e),
            e => Assert.IsType<ChamadoAtribuido>(e));

        chamado.ClearDomainEvents();

        Assert.Empty(chamado.DomainEvents);
    }
}
