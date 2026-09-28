using Chamados.Application.Queries;
using Chamados.Application.UnitTests.Fakes;
using Chamados.Contracts;
using SharedKernel;
using Xunit;

namespace Chamados.Application.UnitTests.Queries;

/// <summary>
/// MeusChamadosQueryHandler: filtro por linha (arquitetura/14) — o solicitante vem sempre de
/// ICurrentUser, nunca da requisição. A paginação e a contagem total são da porta de leitura
/// (arquitetura/19); o handler repassa o PagedResult sem alterá-lo.
/// </summary>
public class MeusChamadosQueryHandlerTests
{
    [Fact]
    public async Task Deve_consultar_pelo_usuario_autenticado_e_repassar_a_pagina_da_leitura()
    {
        var solicitanteAutenticado = Guid.NewGuid();
        var pagina = new PagedResult<ChamadoResumoDto>([ChamadoResumoFactory.Novo()], TotalItems: 41);
        var leitura = new FakeChamadoLeitura()
            .ComPaginaDoSolicitante(solicitanteAutenticado, pagina)
            .ComPaginaDoSolicitante(Guid.NewGuid(), new PagedResult<ChamadoResumoDto>([ChamadoResumoFactory.Novo()], 1));
        var currentUser = new FakeCurrentUser { UserId = solicitanteAutenticado };
        var handler = new MeusChamadosQueryHandler(leitura, currentUser);

        var resultado = await handler.Handle(new MeusChamadosQuery(Page: 3, PageSize: 20), CancellationToken.None);

        Assert.True(resultado.IsSuccess);
        Assert.Same(pagina, resultado.Value);
        Assert.Equal(41, resultado.Value!.TotalItems);
        Assert.Equal((solicitanteAutenticado, 3, 20), Assert.Single(leitura.ConsultasPorSolicitante));
    }
}
