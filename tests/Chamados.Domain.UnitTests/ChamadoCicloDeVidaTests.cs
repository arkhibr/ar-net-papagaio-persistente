using Chamados.Contracts;
using Chamados.Domain;
using SharedKernel;
using Xunit;

namespace Chamados.Domain.UnitTests;

/// <summary>
/// Transições de ciclo de vida do Chamado (especificacao-clarificada.md,
/// "Ciclo de vida de Chamado (atualizado)"; plano-de-arquitetura.md secao 2):
///
///   Aberto --Atribuir--> EmAtendimento
///   EmAtendimento --Devolver--> Aberto
///   EmAtendimento --Resolver (nota obrigatória)--> Resolvido
///   Resolvido --Fechar--> Fechado
///   Fechado --Reabrir (só dentro de 5 dias corridos do fechamento)--> Aberto
///
/// Transições fora dessas combinações devem lançar DomainException — nunca
/// uma exceção genérica, e a checagem de estado é invariante do agregado, não
/// validação sintática de Command (essa distinção é responsabilidade do handler
/// de Application, não testada aqui).
/// </summary>
public class ChamadoCicloDeVidaTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    private static Chamado NovoChamado() =>
        Chamado.Abrir(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);

    private static Chamado ChamadoEmAtendimento(out Guid tecnicoId)
    {
        var chamado = NovoChamado();
        tecnicoId = Guid.NewGuid();
        chamado.Atribuir(tecnicoId);
        return chamado;
    }

    private static Chamado ChamadoResolvido(DateTimeOffset resolvidoEm)
    {
        var chamado = ChamadoEmAtendimento(out _);
        chamado.Resolver("Reiniciei o serviço e o problema não voltou a ocorrer.", resolvidoEm);
        return chamado;
    }

    private static Chamado ChamadoFechado(DateTimeOffset resolvidoEm, DateTimeOffset fechadoEm)
    {
        var chamado = ChamadoResolvido(resolvidoEm);
        chamado.Fechar(fechadoEm);
        return chamado;
    }

    // --- Atribuir: Aberto -> EmAtendimento ---

    [Fact]
    public void Atribuir_a_partir_de_Aberto_deve_mover_para_EmAtendimento_e_gravar_o_tecnico()
    {
        var chamado = NovoChamado();
        var tecnicoId = Guid.NewGuid();

        chamado.Atribuir(tecnicoId);

        Assert.Equal(StatusChamado.EmAtendimento, chamado.Status);
        Assert.Equal(tecnicoId, chamado.TecnicoAtribuidoId);
    }

    [Fact]
    public void Atribuir_a_partir_de_EmAtendimento_deve_lancar_DomainException()
    {
        var chamado = ChamadoEmAtendimento(out _);

        Assert.Throws<DomainException>(() => chamado.Atribuir(Guid.NewGuid()));
    }

    [Fact]
    public void Atribuir_a_partir_de_Resolvido_deve_lancar_DomainException()
    {
        var chamado = ChamadoResolvido(AbertoEm.AddHours(1));

        Assert.Throws<DomainException>(() => chamado.Atribuir(Guid.NewGuid()));
    }

    [Fact]
    public void Atribuir_a_partir_de_Fechado_deve_lancar_DomainException()
    {
        var chamado = ChamadoFechado(AbertoEm.AddHours(1), AbertoEm.AddHours(2));

        Assert.Throws<DomainException>(() => chamado.Atribuir(Guid.NewGuid()));
    }

    // --- Devolver: EmAtendimento -> Aberto ---

    [Fact]
    public void Devolver_a_partir_de_EmAtendimento_deve_mover_para_Aberto()
    {
        var chamado = ChamadoEmAtendimento(out _);

        chamado.Devolver();

        Assert.Equal(StatusChamado.Aberto, chamado.Status);
    }

    [Fact]
    public void Devolver_a_partir_de_Aberto_deve_lancar_DomainException()
    {
        var chamado = NovoChamado();

        Assert.Throws<DomainException>(() => chamado.Devolver());
    }

    [Fact]
    public void Devolver_a_partir_de_Resolvido_deve_lancar_DomainException()
    {
        var chamado = ChamadoResolvido(AbertoEm.AddHours(1));

        Assert.Throws<DomainException>(() => chamado.Devolver());
    }

    [Fact]
    public void Devolver_a_partir_de_Fechado_deve_lancar_DomainException()
    {
        var chamado = ChamadoFechado(AbertoEm.AddHours(1), AbertoEm.AddHours(2));

        Assert.Throws<DomainException>(() => chamado.Devolver());
    }

    // --- Resolver: só a partir de EmAtendimento, exige nota não vazia ---

    [Fact]
    public void Resolver_a_partir_de_EmAtendimento_com_nota_deve_mover_para_Resolvido_e_gravar_a_nota_e_a_data()
    {
        var chamado = ChamadoEmAtendimento(out _);
        var resolvidoEm = AbertoEm.AddHours(3);

        chamado.Resolver("Reiniciei o serviço.", resolvidoEm);

        Assert.Equal(StatusChamado.Resolvido, chamado.Status);
        Assert.Equal("Reiniciei o serviço.", chamado.NotaResolucao);
        Assert.Equal(resolvidoEm, chamado.ResolvidoEm);
    }

    [Fact]
    public void Resolver_a_partir_de_Aberto_deve_lancar_DomainException()
    {
        var chamado = NovoChamado();

        Assert.Throws<DomainException>(() => chamado.Resolver("Nota qualquer.", AbertoEm.AddHours(1)));
    }

    [Fact]
    public void Resolver_a_partir_de_Resolvido_deve_lancar_DomainException()
    {
        var chamado = ChamadoResolvido(AbertoEm.AddHours(1));

        Assert.Throws<DomainException>(() => chamado.Resolver("Nota qualquer.", AbertoEm.AddHours(2)));
    }

    [Fact]
    public void Resolver_a_partir_de_Fechado_deve_lancar_DomainException()
    {
        var chamado = ChamadoFechado(AbertoEm.AddHours(1), AbertoEm.AddHours(2));

        Assert.Throws<DomainException>(() => chamado.Resolver("Nota qualquer.", AbertoEm.AddHours(3)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolver_com_nota_de_resolucao_vazia_ou_em_branco_deve_lancar_DomainException(string notaInvalida)
    {
        var chamado = ChamadoEmAtendimento(out _);

        Assert.Throws<DomainException>(() => chamado.Resolver(notaInvalida, AbertoEm.AddHours(1)));
    }

    [Fact]
    public void Resolver_com_nota_nula_deve_lancar_DomainException()
    {
        var chamado = ChamadoEmAtendimento(out _);

        Assert.Throws<DomainException>(() => chamado.Resolver(null!, AbertoEm.AddHours(1)));
    }

    // --- Fechar: Resolvido -> Fechado ---

    [Fact]
    public void Fechar_a_partir_de_Resolvido_deve_mover_para_Fechado_e_gravar_a_data()
    {
        var chamado = ChamadoResolvido(AbertoEm.AddHours(1));
        var fechadoEm = AbertoEm.AddHours(2);

        chamado.Fechar(fechadoEm);

        Assert.Equal(StatusChamado.Fechado, chamado.Status);
        Assert.Equal(fechadoEm, chamado.FechadoEm);
    }

    [Fact]
    public void Fechar_a_partir_de_Aberto_deve_lancar_DomainException()
    {
        var chamado = NovoChamado();

        Assert.Throws<DomainException>(() => chamado.Fechar(AbertoEm.AddHours(1)));
    }

    [Fact]
    public void Fechar_a_partir_de_EmAtendimento_deve_lancar_DomainException()
    {
        var chamado = ChamadoEmAtendimento(out _);

        Assert.Throws<DomainException>(() => chamado.Fechar(AbertoEm.AddHours(1)));
    }

    [Fact]
    public void Fechar_a_partir_de_Fechado_deve_lancar_DomainException()
    {
        var chamado = ChamadoFechado(AbertoEm.AddHours(1), AbertoEm.AddHours(2));

        Assert.Throws<DomainException>(() => chamado.Fechar(AbertoEm.AddHours(3)));
    }

    // --- Reabrir: Fechado -> Aberto, só dentro de 5 dias corridos do fechamento ---

    [Fact]
    public void Reabrir_dentro_da_janela_de_5_dias_corridos_deve_mover_para_Aberto()
    {
        var fechadoEm = AbertoEm.AddHours(2);
        var chamado = ChamadoFechado(AbertoEm.AddHours(1), fechadoEm);

        chamado.Reabrir(fechadoEm.AddDays(5));

        Assert.Equal(StatusChamado.Aberto, chamado.Status);
    }

    [Fact]
    public void Reabrir_exatamente_no_limite_da_janela_deve_ser_permitido()
    {
        var fechadoEm = AbertoEm.AddHours(2);
        var chamado = ChamadoFechado(AbertoEm.AddHours(1), fechadoEm);

        var exception = Record.Exception(() => chamado.Reabrir(fechadoEm.AddDays(5)));

        Assert.Null(exception);
    }

    [Fact]
    public void Reabrir_apos_a_janela_de_5_dias_corridos_deve_lancar_DomainException()
    {
        var fechadoEm = AbertoEm.AddHours(2);
        var chamado = ChamadoFechado(AbertoEm.AddHours(1), fechadoEm);

        Assert.Throws<DomainException>(() => chamado.Reabrir(fechadoEm.AddDays(5).AddSeconds(1)));
    }

    [Fact]
    public void Reabrir_a_partir_de_Aberto_deve_lancar_DomainException()
    {
        var chamado = NovoChamado();

        Assert.Throws<DomainException>(() => chamado.Reabrir(AbertoEm.AddDays(1)));
    }

    [Fact]
    public void Reabrir_a_partir_de_EmAtendimento_deve_lancar_DomainException()
    {
        var chamado = ChamadoEmAtendimento(out _);

        Assert.Throws<DomainException>(() => chamado.Reabrir(AbertoEm.AddDays(1)));
    }

    [Fact]
    public void Reabrir_a_partir_de_Resolvido_deve_lancar_DomainException()
    {
        var chamado = ChamadoResolvido(AbertoEm.AddHours(1));

        Assert.Throws<DomainException>(() => chamado.Reabrir(AbertoEm.AddDays(1)));
    }
}
