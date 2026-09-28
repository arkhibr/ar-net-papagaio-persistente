using Chamados.Application.Queries;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using SharedKernel;
using Xunit;

namespace Chamados.Application.UnitTests.Queries;

/// <summary>
/// ObterChamadoQueryHandler: a autorização já rodou no pipeline (ObterChamadoQuery
/// .IsAuthorizedAsync, coberta em ObterChamadoQueryAuthorizationTests; M3 de achados.md), então
/// o handler só repassa o detalhe versionado da porta de leitura (arquitetura/27). Chamado
/// inexistente vira Result.NotFound (ErrorKind.NotFound -> 404 sem depender do texto).
/// </summary>
public class ObterChamadoQueryHandlerTests
{
    private static readonly DateTimeOffset AbertoEm = new(2026, 9, 13, 8, 0, 0, TimeSpan.Zero);

    private static ChamadoDetalheVersionado NovoDetalhe(string versao) =>
        new(
            new ChamadoDetalheDto(
                Id: Guid.NewGuid(),
                SolicitanteId: Guid.NewGuid(),
                CategoriaId: Guid.NewGuid(),
                EquipeId: Guid.NewGuid(),
                Prioridade: PrioridadeChamado.Media,
                Status: StatusChamado.Aberto,
                AbertoEm: AbertoEm,
                PrazoSla: AbertoEm.AddHours(24),
                TecnicoAtribuidoId: null,
                NotaResolucao: null,
                ResolvidoEm: null,
                FechadoEm: null,
                Escalonado: false,
                DataEscalonamento: null),
            versao);

    [Fact]
    public async Task Deve_devolver_o_detalhe_e_a_versao_lidos_da_porta_de_leitura()
    {
        var detalhe = NovoDetalhe(Convert.ToBase64String([9, 9, 9]));
        var leitura = new FakeChamadoLeitura().ComDetalhe(detalhe);
        var handler = new ObterChamadoQueryHandler(leitura);

        var resultado = await handler.Handle(new ObterChamadoQuery(detalhe.Chamado.Id), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Same(detalhe, resultado.Value);
        // A versão (base64) segue separada do DTO: a Api a expõe só como ETag (arquitetura/06).
        Assert.Equal(Convert.ToBase64String([9, 9, 9]), resultado.Value!.Versao);
    }

    [Fact]
    public async Task Chamado_inexistente_deve_devolver_Result_NotFound()
    {
        var handler = new ObterChamadoQueryHandler(new FakeChamadoLeitura());

        var resultado = await handler.Handle(new ObterChamadoQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorKind.NotFound, resultado.ErrorKind);
    }
}
