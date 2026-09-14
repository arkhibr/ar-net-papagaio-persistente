using Chamados.Application.Commands;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using Chamados.Domain;
using Xunit;

namespace Chamados.Application.UnitTests.Commands;

/// <summary>
/// AtribuirChamadoCommandHandler (modo TDD): carrega o Chamado via ObterParaEscritaAsync,
/// chama Chamado.Atribuir(TecnicoId), captura DomainException e converte para Result.Failure
/// (arquitetura/06). Conflito de RowVersion não é responsabilidade deste handler: propaga
/// como ConcurrencyException a partir do SaveChanges do UnitOfWorkBehavior — não testado
/// aqui (isso é Infrastructure.IntegrationTests), só documentado.
/// </summary>
public class AtribuirChamadoCommandHandlerTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Atribuir_um_chamado_Aberto_deve_mover_para_EmAtendimento_com_o_tecnico_informado()
    {
        var chamado = Chamado.Abrir(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var handler = new AtribuirChamadoCommandHandler(repository);
        var tecnicoId = Guid.NewGuid();

        var command = new AtribuirChamadoCommand(chamado.Id, tecnicoId, RowVersion: [1], IdempotencyKey: "chave-atribuir-1");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(StatusChamado.EmAtendimento, chamado.Status);
        Assert.Equal(tecnicoId, chamado.TecnicoAtribuidoId);
        // RowVersion do Command (decodificado pelo controller a partir do If-Match) precisa
        // chegar à porta de repositório — é isso que fecha o ciclo real de ETag/If-Match.
        Assert.Equal([(byte)1], repository.RowVersionEsperadoRecebido[chamado.Id]);
    }

    [Fact]
    public async Task Atribuir_um_chamado_ja_EmAtendimento_deve_devolver_Result_Failure_sem_deixar_a_DomainException_escapar()
    {
        var chamado = Chamado.Abrir(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PrioridadeChamado.Media, horasDeSla: 24, AbertoEm);
        chamado.Atribuir(Guid.NewGuid());
        var repository = new FakeChamadoRepository().ComChamado(chamado);
        var handler = new AtribuirChamadoCommandHandler(repository);

        var command = new AtribuirChamadoCommand(chamado.Id, Guid.NewGuid(), RowVersion: [1], IdempotencyKey: "chave-atribuir-2");

        var resultado = await handler.Handle(command, CancellationToken.None);

        Assert.True(resultado.IsFailure);
    }
}
