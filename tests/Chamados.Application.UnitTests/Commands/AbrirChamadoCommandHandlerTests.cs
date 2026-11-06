using Catalogo.Contracts;
using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Chamados.Domain;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// AbrirChamadoCommandHandler (modo TDD): descreve o comportamento esperado a partir de
/// plano-de-arquitetura.md (secao 2/3/5) e especificacao-clarificada.md, não da
/// implementação (que ainda não existe — handler é stub lançando NotImplementedException).
///
/// Comportamento esperado: consulta Catalogo.Contracts.ResolverEquipeESlaQuery para resolver
/// EquipeId/HorasDeSla por categoria+prioridade, congela o snapshot chamando Chamado.Abrir(...)
/// com o DateTimeOffset resolvido do TimeProvider, e persiste via IChamadoRepository.
///
/// Correção registrada em especificacao-clarificada.md ("Quem define prioridade"): o
/// solicitante escolhe a prioridade explicitamente na abertura — AbrirChamadoCommand carrega
/// um campo Prioridade (Chamados.Domain.PrioridadeChamado) e o handler repassa essa escolha
/// para ResolverEquipeESlaQuery (traduzida para Catalogo.Contracts.PrioridadeServico), nunca
/// um valor fixo (Media) hardcoded no handler.
/// </summary>
public class AbrirChamadoCommandHandlerTests
{
    [Theory]
    [InlineData(PrioridadeChamado.Media, PrioridadeServico.Media, 24)]
    [InlineData(PrioridadeChamado.Critica, PrioridadeServico.Critica, 4)]
    [InlineData(PrioridadeChamado.Baixa, PrioridadeServico.Baixa, 72)]
    public async Task Abrir_deve_consultar_o_catalogo_com_a_prioridade_escolhida_pelo_solicitante_no_command(
        PrioridadeChamado prioridadeEscolhida, PrioridadeServico prioridadeEsperadaNaConsulta, int horasDeSla)
    {
        var categoriaId = Guid.NewGuid();
        var equipeId = Guid.NewGuid();
        var solicitanteId = Guid.NewGuid();
        var agora = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

        var repository = new FakeChamadoRepository();
        var sender = new FakeSender()
            .ComResposta(categoriaId, prioridadeEsperadaNaConsulta, equipeId, horasDeSla);
        var timeProvider = new FixedTimeProvider(agora);
        var handler = new AbrirChamadoCommandHandler(repository, sender, timeProvider);

        var command = new AbrirChamadoCommand(solicitanteId, categoriaId, prioridadeEscolhida, "chave-abrir-1");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var chamadoCriado = Assert.Single(repository.Todos);
        Assert.Equal(solicitanteId, chamadoCriado.SolicitanteId);
        Assert.Equal(categoriaId, chamadoCriado.CategoriaId);
        Assert.Equal(equipeId, chamadoCriado.EquipeId);
        Assert.Equal(prioridadeEscolhida, chamadoCriado.Prioridade);
        Assert.Equal(agora.AddHours(horasDeSla), chamadoCriado.PrazoSla);
    }

    [Fact]
    public async Task Abrir_nao_deve_usar_prioridade_fixa_quando_o_catalogo_so_responde_para_a_prioridade_do_command()
    {
        // Guarda contra regressão: se o handler voltar a hardcodar PrioridadeServico.Media na
        // consulta, esta configuração (resposta só para Critica) faz o Catalogo "não encontrar"
        // a categoria e o Result vir Failure, mesmo a categoria existindo de fato.
        var categoriaId = Guid.NewGuid();
        var equipeId = Guid.NewGuid();
        var solicitanteId = Guid.NewGuid();

        var repository = new FakeChamadoRepository();
        var sender = new FakeSender().ComResposta(categoriaId, PrioridadeServico.Critica, equipeId, horasDeSla: 4);
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero));
        var handler = new AbrirChamadoCommandHandler(repository, sender, timeProvider);

        var command = new AbrirChamadoCommand(solicitanteId, categoriaId, PrioridadeChamado.Critica, "chave-abrir-3");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        var chamadoCriado = Assert.Single(repository.Todos);
        Assert.Equal(PrioridadeChamado.Critica, chamadoCriado.Prioridade);
    }

    [Fact]
    public async Task Abrir_com_categoria_inexistente_no_catalogo_deve_devolver_Result_Failure()
    {
        var repository = new FakeChamadoRepository();
        var sender = new FakeSender(); // nenhuma resposta configurada -> Catalogo não encontra a categoria
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero));
        var handler = new AbrirChamadoCommandHandler(repository, sender, timeProvider);

        var command = new AbrirChamadoCommand(Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, "chave-abrir-2");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Empty(repository.Todos);
    }
}
